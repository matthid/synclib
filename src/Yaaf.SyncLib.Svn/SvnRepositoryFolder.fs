// ----------------------------------------------------------------------------
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.
// ----------------------------------------------------------------------------
namespace Yaaf.SyncLib.Svn

open Yaaf.SyncLib
open Yaaf.SyncLib.Helpers
open Yaaf.SyncLib.Helpers.AsyncTrace
open Yaaf.SyncLib.Svn

open System.IO

/// Syncronises a git folder
type SvnRepositoryFolder(folder:ManagedFolderInfo) as x =  
    inherit RepositoryFolder(folder)

    let localWatcher = new SimpleLocalChangeWatcher(folder.FullPath, (fun err -> x.ReportError err))
    let remoteWatcher = new RemoteChangeWatcher(folder)

    let progressChanged = new Event<double>()
    let syncConflict = new Event<SyncConflict>()

    /// Indicates wheter this svn rep is initialized (ie requires special checks at startup)
    let mutable isInit = false
    let svnPath = folder.Additional.["svnpath"]

    let init() = asyncTrace() {
        let! (t:ITracer) = AsyncTrace.traceInfo()
        t.logInfo "Init SVN Repro %s" folder.Name
        // Check if repro is initialized (ie is a git repro)
        try
            do! SvnProcess.status svnPath (folder.FullPath) |> AsyncTrace.Ignore
        with
        | SvnNotWorkingDir ->
            t.logWarn "%s is no SVN Repro so init it" folder.Name
            do! SvnProcess.checkout svnPath folder.FullPath folder.Remote
                
                
        
        // Check whether the remote url matches
        let! svnInfo = SvnProcess.info svnPath (folder.FullPath)
        if (svnInfo.Url <> folder.Remote) then failwith (invalidOp "SVN Url does not match!")

        isInit <- true
    }

    let resolveConflicts () = asyncTrace() {
        return ()
    }

    let syncDown() = asyncTrace() {
        let! (t:ITracer) = AsyncTrace.traceInfo()
        progressChanged.Trigger 0.0
        try
            if not isInit then do! init()

            t.logInfo "Starting SVN Syncdown of %s" folder.Name
            /// counting the items to update
            let! items = SvnProcess.status svnPath (folder.FullPath)
            let updateItemsCount =
                items
                    |> Seq.filter (fun item -> item.IsOutOfDate)
                    |> Seq.length
                    |> float

            // Starting the update
            let finishedFileCount = ref 0
            let conflictFile = ref false
            do! SvnProcess.update 
                    svnPath 
                    folder.FullPath
                    (fun updateFinished ->
                        match updateFinished with
                        | FinishedFile(updateType, file) ->
                            match updateType with
                            | SvnUpdateType.Conflicting -> 
                                syncConflict.Trigger (SyncConflict.Unknown (sprintf "file %s is conflicting" file))
                                conflictFile := true
                            | _ -> ()
                            finishedFileCount := !finishedFileCount + 1
                            progressChanged.Trigger (0.95 * float (!finishedFileCount) / updateItemsCount)
                        | _ -> ())

            // Conflict resolution
            if (!conflictFile) then
                // Resolve conflict
                do! resolveConflicts()
        finally 
            progressChanged.Trigger 1.0
    }

    let syncUp() = asyncTrace() {
        return ()
    }    
    
    override x.StartSyncDown () = 
        syncDown()

    override x.StartSyncUp() = 
        syncUp()
        
    [<CLIEvent>]
    override x.ProgressChanged = progressChanged.Publish

    [<CLIEvent>]
    override x.SyncConflict = syncConflict.Publish