﻿namespace SyncLib.Git

open SyncLib

type GitRepositoryFolder(folder:ManagedFolderInfo) as x =  
    inherit RepositoryFolder(folder, new LocalChangeWatcher(folder), new RemoteChangeWatcher(folder))
    let progressChanged = new Event<double>()
    let syncConflict = new Event<SyncConflict>()
    let remoteName = "synclib"
    /// Indicates wheter this git rep is initialized (ie requires)
    let mutable isInit = false

    let init() = 
        async {
            // Check if repro is initialized (ie is a git repro)
            try
                do! GitProcess.RunGitStatusAsync(folder.FullPath) |> Async.Ignore
            with
            | GitProcessFailed(s) when s.Contains "fatal: Not a git repository (or any of the parent directories): .git" ->
                do! GitProcess.RunGitInitAsync(folder.FullPath)

            // Check whether the "sparkle" remote point exists
            let! remotes = GitProcess.RunGitRemoteAsync(folder.FullPath, ListVerbose)
            let syncRemotes = 
                remotes 
                    |> Seq.filter (fun t -> t.Name = remoteName)
                    |> Seq.toArray
            if (remotes.Count < 2 || remotes |> Seq.exists (fun t -> t.Url <> folder.Remote)) then
                if (remotes.Count > 0) then
                    do! GitProcess.RunGitRemoteAsync(folder.FullPath, Remove(remoteName)) |> Async.Ignore

                do! GitProcess.RunGitRemoteAsync(folder.FullPath, Add(remoteName, folder.Remote)) |> Async.Ignore
            
            isInit <- true
            return ()
        }


    let syncDown() =
        async {
            progressChanged.Trigger 0.0
            if not isInit then do! init()

            // Fetch changes
            do! GitProcess.RunGitFetchAsync(
                    folder.FullPath, 
                    folder.Remote, 
                    "master", 
                    (fun newProgress -> progressChanged.Trigger newProgress))
                |> Async.Ignore

            progressChanged.Trigger 1.0

            // Merge changes into local directory via "git rebase FETCH_HEAD"
            try
                do! GitProcess.RunGitRebaseAsync(folder.FullPath, "FETCH_HEAD", "master")
            with
                | GitProcessFailed(s) ->
                    // Conflict
                    syncConflict.Trigger (SyncConflict.Unknown s)

                    // TODO: Resolve conflict
                    //do! GitProcess.RunGitInitAsync(folder.FullPath) 
        }
    let syncUp() = async {
            try
                // TODO: Push to server
                do ()
            with 
                | GitProcessFailed(s) ->
                    // Conflict
                    syncConflict.Trigger (SyncConflict.Unknown s)
                    x.RequestSyncDown() // We handle conflicts there
                    x.RequestSyncUp()
            
            return ()
        }

    override x.StartSyncDown () = 
        syncDown() |> Async.StartAsTask

    override x.StartSyncUp() = 
        syncUp() |> Async.StartAsTask
        
    [<CLIEvent>]
    override x.ProgressChanged = progressChanged.Publish

    [<CLIEvent>]
    override x.SyncConflict = syncConflict.Publish