// ----------------------------------------------------------------------------
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.
// ----------------------------------------------------------------------------
namespace Yaaf.SyncLib.Git

open Yaaf.SyncLib
open Yaaf.SyncLib.Git
open Yaaf.AsyncTrace

open System.IO

/// Syncronises a git folder
type GitRepositoryFolder(folder:ManagedFolderInfo) as x =  
    inherit RepositoryFolder(folder)

    let localWatcher = new SimpleLocalChangeWatcher(folder.FullPath, (fun err -> x.ReportError err))
    let remoteEvent = 
        folder.Additional
            |> RemoteConnectionManager.extractRemoteConnectionData
            |> RemoteConnectionManager.calculateMergedEvent
    let progressChanged = new Event<double>()
    let syncConflict = new Event<SyncConflict>()
    let remoteName = "synclib"
    /// Indicates wheter this git rep is initialized (ie requires)
    let mutable isInit = false
    let git = folder.Additional.["gitpath"]
    let sshPath = folder.Additional.["sshpath"]
    
    // Starts watching the given Changewatcher (uses the given processor-message)
    

        
    do 
        // Start watching
        localWatcher.Changed
            // Filter git directory
            |> Event.filter 
                (fun (changeType, oldPath, newPath)-> 
                    let gitPath = Path.Combine(folder.FullPath, ".git")
                    not (oldPath.StartsWith gitPath) && not (newPath.StartsWith gitPath))
            // Reduce event
            |> Event.reduceTime (System.TimeSpan.FromMinutes(1.0))
            |> Event.add (fun args -> 
                x.RequestSyncUp())

        remoteEvent
                |> Event.add x.RequestSyncDown

    let toSshPath (remote:string) = 
        let remote = 
            if remote.StartsWith("ssh://") then remote.Substring(6)
            else remote

        let d = remote.IndexOf(':')
        if (d <> -1) then
            // git@blub:repro.git
            
            // NOTE: I assume it it possible to have a path like user:pass@blub:repro,
            // however this should not be supported anyway.
            remote.Substring(0, d)
        else
            // git@blub/repro
            remote.Substring(0, remote.IndexOf('/'))
            

    /// Resolves Conflicts and 
    let resolveConflicts() = asyncTrace() {
        let! (t:ITracer) = AsyncTrace.TraceInfo()
        t.logVerb "Resolving GIT Conflicts in %s" folder.Name
        let! fileStatus = GitProcess.RunGitStatusAsync git (folder.FullPath)
        for f in fileStatus do
            if (f.Local.Path.EndsWith(".sparkleshare") || f.Local.Path.EndsWith(".empty")) then
                do! GitProcess.RunGitCheckoutAsync git (folder.FullPath) (CheckoutType.Conflict(Theirs, [f.Local.Path]))
            // Both modified, copy server version to a new file
            else if (f.Local.Status = GitStatusType.Updated || f.Local.Status = GitStatusType.Added)
                    && (f.Server.Status = GitStatusType.Updated || f.Server.Status = GitStatusType.Added) 
            then
                // Copy ours to conflicting
                do! GitProcess.RunGitCheckoutAsync git (folder.FullPath) (CheckoutType.Conflict(Ours, [f.Local.Path]))
                let newName = 
                    sprintf "%s (conflicting on %s)%s"
                        (Path.GetFileNameWithoutExtension f.Local.Path)
                        (System.DateTime.Now.ToString("yyyy-MM-dd_hh-mm-ss"))
                        (Path.GetExtension f.Local.Path)
                let newFile = 
                    Path.Combine(
                        Path.GetDirectoryName(f.Local.Path), newName)
                File.Move(
                    Path.Combine(folder.FullPath, f.Local.Path),     
                    Path.Combine(folder.FullPath, newFile))
                // Use theirs
                do! GitProcess.RunGitCheckoutAsync git (folder.FullPath) (CheckoutType.Conflict(Theirs, [f.Local.Path]))
                syncConflict.Trigger (MergeConflict(f.Local.Path))
            else if (f.Local.Status = GitStatusType.Deleted
                    && f.Server.Status = GitStatusType.Updated)
            then
                do! GitProcess.RunGitAddAsync git (folder.FullPath) (GitAddType.NoOptions) ([f.Local.Path])
            
        do! GitProcess.RunGitAddAsync git (folder.FullPath) (GitAddType.All) ([])

        // Here should be no more conflicts
        // TODO: Check if there are and throw exception if there are
    }

    /// Adds all files to the index and does a commit to the repro
    let commitAllChanges() = asyncTrace() {
        let! (t:ITracer) = AsyncTrace.TraceInfo()
        t.logVerb "Commiting All Changes in %s" folder.Name
        // Add all files
        do! GitProcess.RunGitAddAsync git (folder.FullPath) (GitAddType.All) ([])
        // procude a commit message
        let! changes = GitProcess.RunGitStatusAsync git (folder.FullPath)
        let normalizedChanges =
            changes
                |> Seq.filter (fun f -> match f.Local.Status with
                                            | GitStatusType.Added | GitStatusType.Modified
                                            | GitStatusType.Deleted | GitStatusType.Renamed -> true
                                            | _ -> false)
                |> Seq.map  
                    (fun f -> 
                        { 
                        ChangeType = 
                            match f.Local.Status with
                            | GitStatusType.Added -> CommitMessageChangeType.Added
                            | GitStatusType.Modified -> CommitMessageChangeType.Updated
                            | GitStatusType.Deleted -> CommitMessageChangeType.Deleted
                            | GitStatusType.Renamed -> CommitMessageChangeType.Renamed
                            | _ -> failwith "GIT: got a status that was already filtered"
                        FilePath = f.Local.Path
                        FilePathRename = f.Server.Path
                        })
       

        let commitMessage = 
             x.GenerateCommitMessage normalizedChanges

        if changes.Count > 0 then
            // Do the commit
            do! GitProcess.RunGitCommitAsync git (folder.FullPath) (commitMessage)
    }

    /// Initialize the repository
    let init() = asyncTrace() {
        let! (t:ITracer) = AsyncTrace.TraceInfo()
        t.logInfo "Init GIT Repro %s" folder.Name
        // Check if repro is initialized (ie is a git repro)
        try
            do! GitProcess.RunGitStatusAsync git (folder.FullPath) |> AsyncTrace.Ignore
        with
        | ToolProcessFailed(code, cmd, output, error) 
            when error.Contains "fatal: Not a git repository (or any of the parent directories): .git" ->
                t.logWarn "%s is no GIT Repro so init one" folder.Name
                do! GitProcess.RunGitInitAsync git (folder.FullPath)
                
                    
           
        // Check ssh connection
        do! SshProcess.ensureConnection sshPath folder.FullPath (toSshPath folder.Remote) false

        // Check whether the "synclib" remote point exists
        let! remotes = GitProcess.RunGitRemoteAsync git (folder.FullPath) (ListVerbose)
        let syncRemotes = 
            remotes 
                |> Seq.filter (fun t -> t.Name = remoteName)
                |> Seq.toArray
        if (syncRemotes.Length < 2 || syncRemotes |> Seq.exists (fun t -> t.Url <> folder.Remote)) then
            if (syncRemotes.Length > 0) then
                t.logWarn "%s has invalid %s remote entry - removing" folder.Name remoteName
                do! GitProcess.RunGitRemoteAsync git folder.FullPath (Remove(remoteName)) |> AsyncTrace.Ignore
                
            t.logInfo "adding %s remote entry to %s" remoteName folder.Name
            do! GitProcess.RunGitRemoteAsync git folder.FullPath (RemoteType.Add(remoteName, folder.Remote)) |> AsyncTrace.Ignore
            
        isInit <- true
    }

    
    let repairMasterBranch () = 
        File.WriteAllText(
            Path.Combine(folder.FullPath, "Readme.synclib"), 
            "This file was required for the first commit\n" +
            "You can safely remove it any time" ) 

    /// The SyncDown Process
    let syncDown() = asyncTrace() {
        let! (t:ITracer) = AsyncTrace.TraceInfo()
        progressChanged.Trigger 0.0
        try
            if not isInit then do! init()

            t.logInfo "Starting GIT Syncdown of %s" folder.Name

            // Fetch changes
            do! GitProcess.RunGitFetchAsync 
                    git 
                    folder.FullPath 
                    remoteName
                    "master"
                    (fun newProgress -> progressChanged.Trigger (newProgress * 0.95))
                |> AsyncTrace.Ignore

            // Merge changes into local directory via "git rebase FETCH_HEAD"
            do! commitAllChanges()
            try
                t.logInfo "Starting GIT SyncDown-Merging of %s" folder.Name
                do! GitProcess.RunGitRebaseAsync git folder.FullPath (Start("FETCH_HEAD", "master"))
            with
                | ToolProcessFailed(exitCode, cmd, o, e) ->
                    match e with
                    | Contains "fatal: no such branch: master" ->
                        repairMasterBranch()
                        x.RequestSyncDown()
                    | _ ->
                        let errorMsg = (sprintf "Cmd: %s, Code: %d, Output: %s, Error %s" cmd exitCode o e)
                        t.logWarn "Conflict while Down-Merging of %s: %s" folder.Name errorMsg
                        // Conflict
                        syncConflict.Trigger (SyncConflict.Unknown errorMsg)
                    
                        // Resolve conflict
                        // TODO: Catch only required exception and retrow all others 
                        do! resolveConflicts()
                        x.RequestSyncDown()
        finally 
            progressChanged.Trigger 1.0
    }

    /// The Upsync Process
    let syncUp() = asyncTrace() {
        let! (t:ITracer) = AsyncTrace.TraceInfo()
        try
            t.logInfo "Starting GIT SyncUp of %s" folder.Name
            progressChanged.Trigger 0.0

            // Push data up to server
            do! commitAllChanges()
            do! GitProcess.RunGitPushAsync 
                    git 
                    folder.FullPath
                    remoteName
                    "master"
                    (fun newProgress -> progressChanged.Trigger newProgress)
                        
            progressChanged.Trigger 1.0
        with 
            | ToolProcessFailed(exitCode, cmd, o ,e) ->
                match e with
                | Contains "error: src refspec master does not match any" -> 
                    // No Master Branch
                    repairMasterBranch()
                    x.RequestSyncUp()
                | _ ->
                    // Conflict
                    // TODO: Catch only required exception and retrow all others 
                    let errorMsg = (sprintf "Cmd: %s, Code: %d, Output: %s, Error %s" cmd exitCode o e)
                    t.logWarn "Conflict while Sync-Up of %s: %s" folder.Name errorMsg
                    
                    syncConflict.Trigger (SyncConflict.Unknown errorMsg)
                    x.RequestSyncDown() // We handle conflicts there
                    x.RequestSyncUp()
    }

    override x.StartSyncDown () = 
        syncDown()

    override x.StartSyncUp() = 
        syncUp()
        
    [<CLIEvent>]
    override x.ProgressChanged = progressChanged.Publish

    [<CLIEvent>]
    override x.SyncConflict = syncConflict.Publish