// ----------------------------------------------------------------------------
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.
// ----------------------------------------------------------------------------
namespace Yaaf.SyncLib.Svn

open Yaaf.AsyncTrace

open Yaaf.SyncLib
open Yaaf.SyncLib.Svn

open System.IO

/// Syncronises a git folder
type SvnRepositoryFolder(folder:ManagedFolderInfo) as x =  
    inherit RepositoryFolder(folder)

    let localWatcher = new SimpleLocalChangeWatcher(folder.FullPath, (fun err -> x.ReportError err))
    let remoteWatcher = RemoteConnectionManager.getRemoteChanged(folder)

    let progressChanged = new Event<double>()
    let syncConflict = new Event<SyncConflict>()

    /// Indicates wheter this svn rep is initialized (ie requires special checks at startup)
    let mutable isInit = false
    let svnPath = folder.Additional.["svnpath"]
    let invokeSvn f = f svnPath folder.FullPath
    do
        // Start watching
        localWatcher.Changed
            // Filter svn directory
            |> Event.filter 
                (fun (changeType, oldPath, newPath)-> 
                    let gitPath = Path.Combine(folder.FullPath, ".svn")
                    not (oldPath.StartsWith gitPath) && not (newPath.StartsWith gitPath))
            // Reduce event
            |> Event.reduceTime (System.TimeSpan.FromMinutes(1.0))
            |> Event.add (fun args -> 
                x.RequestSyncUp())
        match remoteWatcher with
        | Option.Some event ->
            event 
                |> Event.add 
                    (fun () -> x.RequestSyncDown())
        | Option.None -> ()

    let init() = asyncTrace() {
        let! (t:ITracer) = traceInfo()
        t.logInfo "Init SVN Repro %s" folder.Name
        // Check if repro is initialized (ie is a git repro)
        try
            do! SvnProcess.status |> invokeSvn |> AsyncTrace.Ignore
        with
        | SvnNotWorkingDir ->
            t.logWarn "%s is no SVN Repro so init it" folder.Name
            do! SvnProcess.checkout folder.Remote |> invokeSvn
                
                
        
        // Check whether the remote url matches
        let! svnInfo = SvnProcess.info |> invokeSvn
        if (svnInfo.Url <> folder.Remote) then failwith (invalidOp "SVN Url does not match!")

        isInit <- true
    }

    let resolveConflicts () = asyncTrace() {
        let! items = SvnProcess.status |> invokeSvn
        let conflicting = 
            items
                |> Seq.filter (fun item -> item.ChangeType = SvnStatusLineChangeType.ContentConflict)
        for conflict in conflicting do
            let filePath = conflict.FilePath
            let relpath = Path.GetDirectoryName filePath
            let filename = Path.GetFileNameWithoutExtension filePath
            let extension = Path.GetExtension filePath
            let minename = sprintf "%s%s.mine" filename extension
            let newName = 
                sprintf "%s (conflicting on %s)%s"
                    filename
                    (System.DateTime.Now.ToString("yyyy-MM-dd_hh-mm-ss"))
                    extension
            let conflictname = sprintf "%s%s.r" filename extension
            let maxrev =
                Directory.EnumerateFiles(
                    Path.Combine(folder.FullPath, relpath),
                    sprintf "%s*" conflictname)
                    // Only name
                    |> Seq.map (fun fullpath -> fullpath.Substring(folder.FullPath.Length + 1 + relpath.Length + 1))
                    |> Seq.map 
                        (fun name -> 
                            match name with
                            | StartsWith conflictname rest -> System.Int32.Parse(rest)
                            | _ -> failwith (sprintf "no conflict file %s" name))
                    |> Seq.max
            // Copy my version to "(conflicting)"
            File.Copy(
                Path.Combine(folder.FullPath, relpath, minename),
                Path.Combine(folder.FullPath, relpath, newName),
                true)

            // Copy replace current copy with server version
            File.Copy(
                Path.Combine(folder.FullPath, relpath, sprintf "%s%d" conflictname maxrev),
                Path.Combine(folder.FullPath, filePath),
                true)

            // Mark as solved 
            do! SvnProcess.resolved filePath |> invokeSvn
    }

    let syncDown() = asyncTrace() {
        let! (t:ITracer) = traceInfo()
        progressChanged.Trigger 0.0
        try
            if not isInit then do! init()

            t.logInfo "Starting SVN Syncdown of %s" folder.Name
            /// counting the items to update
            let! items = SvnProcess.status |> invokeSvn
            let updateItemsCount =
                let t =
                    items
                        |> Seq.filter (fun item -> item.IsOutOfDate)
                        |> Seq.length
                if t = 0 then 1.0 else float t
            try
                // Starting the update
                let finishedFileCount = ref 0
                let conflictFile = ref false
                do! SvnProcess.update 
                        (fun updateFinished ->
                            match updateFinished with
                            | FinishedFile(updateType, propType, file) ->
                                match updateType with
                                | SvnUpdateType.Conflicting -> 
                                    syncConflict.Trigger (SyncConflict.Unknown (sprintf "file %s is conflicting" file))
                                    conflictFile := true
                                | _ -> ()
                                finishedFileCount := !finishedFileCount + 1
                                progressChanged.Trigger (0.95 * float (!finishedFileCount) / updateItemsCount)
                            | _ -> ())
                        |> invokeSvn

                // Conflict resolution
                if (!conflictFile) then
                    // Resolve conflict
                    do! resolveConflicts()
            with
                | SvnAlreadyLocked(ToolProcessFailed(code, cmd, o, e)) ->
                    t.logErr "Detected a SVN Workspace Lock (%d, %s, %s, %s)" code cmd o e
                    t.logInfo "Trying to resolve the lock"
                    do! SvnProcess.cleanup |> invokeSvn
                    x.RequestSyncDown() // Try again
        finally 
            progressChanged.Trigger 1.0
    }

    let syncUp() = asyncTrace() {
        // get status
        let! items = SvnProcess.status |> invokeSvn

        // add all changes to svn
        for (toAdd, item) in items
                |> Seq.filter 
                    (fun i -> i.ChangeType = SvnStatusLineChangeType.NotInSourceControl
                           ||i.ChangeType = SvnStatusLineChangeType.ItemMissing)
                |> Seq.map (fun i -> i.ChangeType = SvnStatusLineChangeType.NotInSourceControl, i.FilePath) do
            let f =
                if (toAdd) then SvnProcess.add else SvnProcess.delete

            do! f item |> invokeSvn

        // Get commit message
        let normalizedChanges =
            items
                |> Seq.filter 
                    (fun t ->
                        t.ChangeType = SvnStatusLineChangeType.Added || 
                        t.ChangeType = SvnStatusLineChangeType.Deleted ||
                        t.ChangeType = SvnStatusLineChangeType.ItemMissing || 
                        t.ChangeType = SvnStatusLineChangeType.Modified || 
                        t.ChangeType = SvnStatusLineChangeType.NotInSourceControl || 
                        t.ChangeType = SvnStatusLineChangeType.Replaced)
                |> Seq.map
                    (fun t ->
                        {
                            ChangeType = 
                                match t.ChangeType with
                                | SvnStatusLineChangeType.Added -> CommitMessageChangeType.Added
                                | SvnStatusLineChangeType.Deleted -> CommitMessageChangeType.Deleted
                                | SvnStatusLineChangeType.ItemMissing ->  CommitMessageChangeType.Deleted
                                | SvnStatusLineChangeType.Modified ->  CommitMessageChangeType.Updated
                                | SvnStatusLineChangeType.NotInSourceControl -> CommitMessageChangeType.Added
                                | SvnStatusLineChangeType.Replaced -> CommitMessageChangeType.Updated
                                | _ -> failwith "SVN got a status that was already filtered"
                            FilePath = t.FilePath
                            FilePathRename = ""
                        })
        let commitMessage =
            x.GenerateCommitMessage normalizedChanges
        
        // Do the commit
        do! SvnProcess.commit commitMessage |> invokeSvn
    }    
    
    override x.StartSyncDown () = 
        syncDown()

    override x.StartSyncUp() = 
        syncUp()
        
    [<CLIEvent>]
    override x.ProgressChanged = progressChanged.Publish

    [<CLIEvent>]
    override x.SyncConflict = syncConflict.Publish