﻿namespace SyncLib.Git

open SyncLib
open SyncLib.Helpers
open SyncLib.Helpers.Logger
open SyncLib.Git

type GitRepositoryFolder(folder:ManagedFolderInfo) as x =  
    inherit RepositoryFolder(folder, new LocalChangeWatcher(folder), new RemoteChangeWatcher(folder))
    let progressChanged = new Event<double>()
    let syncConflict = new Event<SyncConflict>()
    let remoteName = "synclib"
    /// Indicates wheter this git rep is initialized (ie requires)
    let mutable isInit = false

    /// Resolves Conflicts and 
    let resoveConflicts() = 
        async {
            logVerb "Resolving Conflicts in %s" folder.Name
            let! fileStatus = GitProcess.RunGitStatusAsync(folder.FullPath)
            for f in fileStatus do
                if (f.Local.Path.EndsWith(".sparkleshare") || f.Local.Path.EndsWith(".empty")) then
                    do! GitProcess.RunGitCheckoutAsync(folder.FullPath, CheckoutType.Conflict(Theirs, [f.Local.Path]))
                // Both modified, copy server version to a new file
                else if (f.Local.Status = GitStatusType.Updated || f.Local.Status = GitStatusType.Added)
                     && (f.Server.Status = GitStatusType.Updated || f.Server.Status = GitStatusType.Added) 
                then
                    do! GitProcess.RunGitCheckoutAsync(folder.FullPath, CheckoutType.Conflict(Theirs, [f.Local.Path]))
                    let newName = 
                        sprintf "%s (conflicting on %s).%s"
                            (System.IO.Path.GetFileNameWithoutExtension f.Local.Path)
                            (System.DateTime.Now.ToString("yyyy-MM-dd_hh-mm-ss"))
                            (System.IO.Path.GetExtension f.Local.Path)
                    
                    System.IO.File.Move(
                        System.IO.Path.Combine(folder.FullPath, f.Local.Path),     
                        System.IO.Path.Combine(folder.FullPath, newName))
                    
                    do! GitProcess.RunGitCheckoutAsync(folder.FullPath, CheckoutType.Conflict(Ours, [f.Local.Path]))
                
                else if (f.Local.Status = GitStatusType.Deleted
                      && f.Server.Status = GitStatusType.Updated)
                then
                    do! GitProcess.RunGitAddAsync(folder.FullPath, GitAddType.NoOptions, [f.Local.Path])
            
            do! GitProcess.RunGitAddAsync(folder.FullPath, GitAddType.All, [])

            // Here should be no more conflicts
            // TODO: Check if there are and throw exception if there are
        }

    /// Adds all files to the index and does a commit to the repro
    let commitAllChanges() =    
        async {
        
            logVerb "Commiting All Changes in %s" folder.Name
            // Add all files
            do! GitProcess.RunGitAddAsync(folder.FullPath, GitAddType.All, [])
            // procude a commit message
            let! changes = GitProcess.RunGitStatusAsync(folder.FullPath)
            let commitMessage =
                changes
                    |> Seq.filter (fun f -> match f.Local.Status with
                                             | GitStatusType.Added | GitStatusType.Modified
                                             | GitStatusType.Deleted | GitStatusType.Renamed -> true
                                             | _ -> false)
                    |> Seq.filter (fun f -> f.Local.Status <> GitStatusType.Modified || not (f.Local.Path.EndsWith(".empty")))
                    |> Seq.map (fun f -> 
                                    if f.Local.Path.EndsWith(".empty") then
                                        { Local = { f.Local with Path = f.Local.Path.Substring(0, 6) };
                                          Server = { f.Server with Path = f.Server.Path.Substring(0, 6) } }
                                    else f
                                    )
                    |> Seq.collect 
                        (fun f ->
                            seq {
                                match f.Local.Status with
                                | GitStatusType.Added -> yield sprintf "+ '%s'" f.Local.Path
                                | GitStatusType.Modified -> yield sprintf "/ '%s'" f.Local.Path
                                | GitStatusType.Deleted -> yield sprintf "- '%s'" f.Local.Path
                                | GitStatusType.Renamed -> 
                                    yield sprintf "- '%s'" f.Local.Path
                                    yield sprintf "+ '%s'" f.Server.Path
                                | _ -> failwith "CRITICAL: got a status that was already filtered"
                            })
                    |> Seq.tryTake 20
                    |> Seq.fold (fun state item -> sprintf "%s\n%s" state item) ""

            let commitMessage = 
                commitMessage + 
                    if (changes.Count > 20) 
                    then "..."
                    else ""

            if changes.Count > 0 then
                // Do the commit
                do! GitProcess.RunGitCommitAsync(folder.FullPath, commitMessage.TrimEnd())
        }

    /// Initialize the repository
    let init() = 
        async {
            logInfo "Init Repro %s" folder.Name
            // Check if repro is initialized (ie is a git repro)
            try
                do! GitProcess.RunGitStatusAsync(folder.FullPath) |> Async.Ignore
            with
            | GitProcessFailed(s) when s.Contains "fatal: Not a git repository (or any of the parent directories): .git" ->
                logInfo "%s is no Repro so init one" folder.Name
                do! GitProcess.RunGitInitAsync(folder.FullPath)

            // Check whether the "sparkle" remote point exists
            let! remotes = GitProcess.RunGitRemoteAsync(folder.FullPath, ListVerbose)
            let syncRemotes = 
                remotes 
                    |> Seq.filter (fun t -> t.Name = remoteName)
                    |> Seq.toArray
            if (remotes.Count < 2 || syncRemotes |> Seq.exists (fun t -> t.Url <> folder.Remote)) then
                if (remotes.Count > 0) then
                    logInfo "%s has invalid %s remote entry - removing" folder.Name remoteName
                    do! GitProcess.RunGitRemoteAsync(folder.FullPath, Remove(remoteName)) |> Async.Ignore
                
                logInfo "adding %s remote entry to %s" remoteName folder.Name
                do! GitProcess.RunGitRemoteAsync(folder.FullPath, RemoteType.Add(remoteName, folder.Remote)) |> Async.Ignore
            
            isInit <- true
            return ()
        }

    let syncDown() =
        async {
            progressChanged.Trigger 0.0
            if not isInit then do! init()

            logInfo "Starting Syncdown of %s" folder.Name

            // Fetch changes
            do! GitProcess.RunGitFetchAsync(
                    folder.FullPath, 
                    folder.Remote, 
                    "master", 
                    (fun newProgress -> progressChanged.Trigger newProgress))
                |> Async.Ignore

            progressChanged.Trigger 1.0

            // Merge changes into local directory via "git rebase FETCH_HEAD"
            do! commitAllChanges()
            try
                logInfo "Starting SyncDown-Merging of %s" folder.Name
                do! GitProcess.RunGitRebaseAsync(folder.FullPath, Start("FETCH_HEAD", "master"))
            with
                | GitProcessFailed(s) ->
                    logWarn "Conflict while Down-Merging of %s" folder.Name
                    // Conflict
                    syncConflict.Trigger (SyncConflict.Unknown s)
                    
                    // Resolve conflict
                    do! resoveConflicts()
        }
    let syncUp() = async {
            try
                logInfo "Starting SyncUp of %s" folder.Name
                progressChanged.Trigger 0.0

                // Push data up to server
                do! commitAllChanges()
                do! GitProcess.RunGitPushAsync(
                        folder.FullPath, 
                        folder.Remote, 
                        "master",
                        (fun newProgress -> progressChanged.Trigger newProgress))
                        
                progressChanged.Trigger 1.0
            with 
                | GitProcessFailed(s) ->
                    // Conflict
                    logWarn "Conflict while Sync-Up of %s: %s" folder.Name s
                    
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