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
    let pushEvent, remoteEvent = 
        folder.Additional
            |> RemoteConnectionManager.extractRemoteConnectionData
            |> RemoteConnectionManager.calculateMergedEvent
            |> x.SetTrace
            |> Async.RunSynchronously
    let progressChanged = new Event<double>()
    let syncConflict = new Event<SyncConflict>()
    let remoteName = "synclib"
    /// Indicates wheter this git rep is initialized (ie requires)
    let mutable isInit = false
    let git = folder.Additional.["gitpath"]
    let sshPath = folder.Additional.["sshpath"]
    let run f = f git folder.FullPath
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
            



    /// Adds all files to the index and does a commit to the repro
    let commitAllChanges() = asyncTrace() {
        let! (t:ITracer) = AsyncTrace.TraceInfo()
        t.logVerb "Commiting All Changes in %s" folder.Name
        // Add all files
        do! GitProcess.add (GitAddType.All) ([]) |> run
        // procude a commit message
        let! changes = GitProcess.status |> run
        let normalizedChanges =
            changes
                // filter only required changes
                |> Seq.filter (fun f -> match f.Local.Status with
                                            | GitStatusType.Added | GitStatusType.Modified
                                            | GitStatusType.Deleted | GitStatusType.Renamed -> true
                                            | _ -> false)
                // map to base class format
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
            do! GitProcess.commit (commitMessage) |> run
    }

    /// Resolves Conflicts and 
    let rec resolveConflicts() = asyncTrace() {
        let! (t:ITracer) = AsyncTrace.TraceInfo()
        t.logVerb "Resolving GIT Conflicts in %s" folder.Name
        let! fileStatus = GitProcess.status |> run
        for f in fileStatus do
            if (f.Local.Path.EndsWith(".sparkleshare") || f.Local.Path.EndsWith(".empty")) then
                do! GitProcess.checkout (CheckoutType.Conflict(Theirs, [f.Local.Path]))  |> run
            // Both modified, copy server version to a new file
            else if (f.Local.Status = GitStatusType.Updated || f.Local.Status = GitStatusType.Added)
                    && (f.Server.Status = GitStatusType.Updated || f.Server.Status = GitStatusType.Added) 
            then
                let checkoutFile fileType =
                    GitProcess.checkout (CheckoutType.Conflict(fileType, [f.Local.Path])) |> run
                let renameResolution toRename toKeep = asyncTrace() {
                    do! checkoutFile toRename
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
                    do! checkoutFile toKeep }
                // NOTE: We are on rebasing, see
                // http://stackoverflow.com/questions/2959443/why-is-the-meaning-of-ours-and-theirs-reversed-with-git-svn
                match x.ConflictStrategy with
                // Copy local version to conflicting
                // Use server version
                | RenameLocal -> do! renameResolution Theirs Ours
                // Copy server version to conflicting
                // Use local version
                | RenameServer -> do! renameResolution Ours Theirs
                // Keep local version and overwrite server
                | KeepLocal -> do! checkoutFile Theirs

                syncConflict.Trigger (MergeConflict(f.Local.Path))
            else if (f.Local.Status = GitStatusType.Deleted
                    && f.Server.Status = GitStatusType.Updated)
            then
                do! GitProcess.add (GitAddType.NoOptions) ([f.Local.Path]) |> run
            
        do! GitProcess.add (GitAddType.All) ([]) |> run
        try
            do! GitProcess.rebase Continue |> run
        with
            | GitMergeConflict ->
                t.logWarn "still conflicts detected... trying to resolve"
                do! resolveConflicts()
    }

    /// Initialize the repository
    let init() = asyncTrace() {
        let! (t:ITracer) = AsyncTrace.TraceInfo()
        t.logInfo "Init GIT Repro %s" folder.Name
        // Check if repro is initialized (ie is a git repro)
        try
            do! GitProcess.status |> run |> AsyncTrace.Ignore
        with
            | GitNoRepository ->
                t.logWarn "no GIT Repro so init one"
                do! GitProcess.init |> run
                
        // Check ssh connection
        try
            do! SshProcess.ensureConnection (toSshPath folder.Remote) sshPath folder.FullPath
        with
            | SshConnectionException(message, sshError) ->
                // most likely Offline
                raise OfflineException
            | SshAuthException(sshError) ->
                t.logErr "error connecting to ssh"
                raise (ConnectionException(sshError))

        // Check whether the "synclib" remote point exists
        let! remotes = GitProcess.remote (ListVerbose) |> run
        let syncRemotes = 
            remotes 
                |> Seq.filter (fun t -> t.Name = remoteName)
                |> Seq.toArray
        if (syncRemotes.Length < 2 || syncRemotes |> Seq.exists (fun t -> t.Url <> folder.Remote)) then
            if (syncRemotes.Length > 0) then
                t.logWarn "%s has invalid %s remote entry - removing" folder.Name remoteName
                do! GitProcess.remote (Remove(remoteName)) |> run |> AsyncTrace.Ignore
                
            t.logInfo "adding %s remote entry to %s" remoteName folder.Name
            do! GitProcess.remote (RemoteType.Add(remoteName, folder.Remote)) |> run |> AsyncTrace.Ignore
            
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
            do! GitProcess.fetch
                    remoteName
                    "master"
                    (fun newProgress -> progressChanged.Trigger (newProgress * 0.95))
                |> run |> AsyncTrace.Ignore

            // Merge changes into local directory via "git rebase FETCH_HEAD"
            do! commitAllChanges()
            try
                t.logInfo "Starting GIT SyncDown-Merging of %s" folder.Name
                do! GitProcess.rebase (Start("FETCH_HEAD", "master")) |> run
            with
                | GitMergeConflict ->
                    t.logWarn "conflicts detected... trying to resolve"
                    do! resolveConflicts()
                | GitUnstagedChanges ->
                    t.logWarn "unstaged changes"
                    x.RequestSyncDown()
                | GitNoMasterBranch ->
                    t.logWarn "fixing no master branch"
                    repairMasterBranch()
                    x.RequestSyncDown()
                | GitPermissionDenied (file) ->
                    t.logWarn "Waiting for filepermission"
                    syncConflict.Trigger (SyncConflict.FileLocked(file))
                    do! GitProcess.rebase Abort |> run
                    do! Async.Sleep 2000 |> AsyncTrace.FromAsync
                    x.RequestSyncDown() // try again
                | GitStuckRebase ->
                    t.logErr "Noticed an invalid state"
                    do! GitProcess.rebase Abort |> run
                    x.RequestSyncDown() // try again
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
            do! GitProcess.push
                    remoteName
                    "master"
                    (fun newProgress -> progressChanged.Trigger newProgress)
                |> run   
            
            // NOTE: Check it there was indeed something pushed
            pushEvent.Trigger("gitupdate")
            progressChanged.Trigger 1.0
        with 
            | GitNoMasterBranch -> 
                t.logWarn "fixing no master branch (on syncup)"
                // No Master Branch
                repairMasterBranch()
                x.RequestSyncUp()               
            | GitRejected ->
                t.logWarn "Push was rejected trying to syncdown"
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