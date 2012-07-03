namespace SyncLib

type ProcessorMessage = 
    | DoSyncUp
    | DoSyncDown

[<AbstractClass>]
type RepositoryFolder(folder : ManagedFolderInfo, localWatcher : IChangeWatcher, remoteWatcher : IChangeWatcher) as x = 
    let syncConflict = new Event<SyncConflict>()
    let syncError = new Event<System.Exception>()
    let syncStateChanged = new Event<SyncState>()
    let localWatcher = localWatcher
    let remoteWatcher = remoteWatcher
    let mutable isStarted = false
    
    let doTask syncState (getTask:unit->System.Threading.Tasks.Task<_>) = 
        async {
            try
                try
                    syncStateChanged.Trigger syncState
                    let t = getTask()
                    // Start if not done already
                    if t.Status = System.Threading.Tasks.TaskStatus.Created then t.Start()
                    do! t |> Async.AwaitTask
                    if t.Exception <> null then syncError.Trigger t.Exception
                finally
                    syncStateChanged.Trigger SyncState.Idle
            with exn -> syncError.Trigger exn
        }
       
    /// This will ensure that only one sync process is running at any time
    let processor = 
        MailboxProcessor<_>.Start(fun inbox -> 
            let rec loop () =
                async {
                    let! msg = inbox.Receive()
                    do!
                        match msg with
                        | DoSyncUp -> 
                            doTask SyncState.SyncUp (fun () -> x.StartSyncUp())
                        | DoSyncDown ->
                            doTask SyncState.SyncDown (fun () -> x.StartSyncDown())
                    return! loop()
                }
            loop () 
            )
    // Starts watching the given Changewatcher (uses the given processor-message)
    let startWatching (watcher:IChangeWatcher) message = 
        watcher.Changed 
            |> Event.filter(fun l -> isStarted)
            |> Event.add (fun l -> processor.Post(message))
    do 
        // Start watching
        startWatching localWatcher DoSyncUp
        startWatching remoteWatcher DoSyncDown

    /// Does a complete sync to the server (Should fail when there are conflicting changes)
    abstract StartSyncUp : unit -> System.Threading.Tasks.Task<unit>

    /// Does a complete sync from the server with possible conflict resolution
    abstract StartSyncDown : unit -> System.Threading.Tasks.Task<unit>

    /// Will be triggered when the Uploadprogress changes
    [<CLIEvent>]
    abstract ProgressChanged : IEvent<double>
    
    /// Will be triggered on Conflicting Files
    [<CLIEvent>]
    abstract SyncConflict : IEvent<SyncConflict>

    interface IManagedFolder with
        member x.StartService () = 
            isStarted <- true

            // Init service with a down and upsync
            processor.Post(DoSyncDown)
            processor.Post(DoSyncUp)
            
            
        member x.StopService () = 
            isStarted <- false

        [<CLIEvent>]
        member x.ProgressChanged = x.ProgressChanged
        
        [<CLIEvent>]
        member x.SyncConflict = x.SyncConflict
        
        [<CLIEvent>]
        member x.SyncError = syncError.Publish

        [<CLIEvent>]
        member x.SyncStateChanged = syncStateChanged.Publish

/// This is a example implementation you can instantly start with
type EmptyRepository(folder:ManagedFolderInfo) =  
    inherit RepositoryFolder(folder, new LocalChangeWatcher(folder), new RemoteChangeWatcher(folder))
    let progressChanged = new Event<double>()
    let syncConflict = new Event<SyncConflict>()

    let syncDown() = async {
            return ()
        }
    let syncUp() = async {
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