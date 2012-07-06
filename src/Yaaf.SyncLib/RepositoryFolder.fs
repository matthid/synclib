// ----------------------------------------------------------------------------
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.
// ----------------------------------------------------------------------------
namespace Yaaf.SyncLib

open Yaaf.SyncLib.Helpers.AsyncTrace
open Yaaf.SyncLib.Helpers

/// Action the RepositoryFolder-Processor should execute
type ProcessorMessage = 
    | DoSyncUp
    | DoSyncDown

/// A IManagedFolder implementation for repositories (git, svn ...)
[<AbstractClass>]
type RepositoryFolder(folder : ManagedFolderInfo, localWatcher : IChangeWatcher, remoteWatcher : IChangeWatcher) as x = 
    let syncConflict = new Event<SyncConflict>()
    let syncError = new Event<System.Exception>()
    let syncStateChanged = new Event<SyncState>()
    let localWatcher = localWatcher
    let remoteWatcher = remoteWatcher
    let mutable isStarted = false
    
    let doTask syncState getTask = 
        asyncTrace() {
            try
                try
                    syncStateChanged.Trigger syncState
                    do! getTask()
                finally
                    syncStateChanged.Trigger SyncState.Idle
            with exn -> syncError.Trigger exn
        }
       
    /// This will ensure that only one sync process is running at any time
    let processor = 
        MailboxProcessor<_>.Start(fun inbox -> 
            let handleMsg msg = 
                asyncTrace() {
                do!
                    match msg with
                    | DoSyncUp -> 
                        doTask SyncState.SyncUp (fun () -> x.StartSyncUp())
                    | DoSyncDown ->
                        doTask SyncState.SyncDown (fun () -> x.StartSyncDown())
                }
            let rec loop i =
                async {
                    try
                        // Get All messages (or wait for the first if non available)
                        let! allmsgs = 
                            if (inbox.CurrentQueueLength = 0) then
                                seq { yield inbox.Receive() }
                            else
                                seq {
                                    for i in 1 .. inbox.CurrentQueueLength do
                                        yield inbox.Receive()
                                }
                            |> Async.Parallel
                        if (isStarted) then
                            // Make sure SyncDowns are prefered over SyncUp
                            // And also make sure we do not spam our queue
                            for msg in
                                allmsgs |> Set.ofSeq  |> Seq.sort |> Seq.toList |> List.rev do
                                let work =
                                    match msg with
                                    | DoSyncUp -> 
                                        doTask SyncState.SyncUp (fun () -> x.StartSyncUp())
                                    | DoSyncDown ->
                                        doTask SyncState.SyncDown (fun () -> x.StartSyncDown())
                                work.SetInfo (new DefaultStateTracer(sprintf "%s(%d): " (match msg with DoSyncUp -> "SyncUp" | DoSyncDown -> "SyncDown") i) :> ITracer)
                                do! work |> convertToAsync 
                    with 
                        | exn -> 
                            printfn "Error on round %d" i
                            syncError.Trigger exn
                    return! loop (i+1)
                }
            loop 0 
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

    /// Requests a UpSync Operation
    member x.RequestSyncUp () = 
        processor.Post(DoSyncUp)

    /// Requests a DownSync Operation
    member x.RequestSyncDown () = 
        processor.Post(DoSyncDown)

    /// Reports an error (this should be protected, but this is not available in F#)
    member x.ReportError exn = syncError.Trigger exn

    /// Does a complete sync to the server (Should fail when there are conflicting changes)
    abstract StartSyncUp : unit -> AsyncTrace<ITracer, unit>

    /// Does a complete sync from the server with possible conflict resolution
    abstract StartSyncDown : unit -> AsyncTrace<ITracer, unit>

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
type EmptyRepository(folder:ManagedFolderInfo) as x =  
    inherit RepositoryFolder(folder, new SimpleLocalChangeWatcher(folder.FullPath, (fun err -> x.ReportError err)), new RemoteChangeWatcher(folder))
    let progressChanged = new Event<double>()
    let syncConflict = new Event<SyncConflict>()

    let syncDown() = asyncTrace() {
            return ()
        }
    let syncUp() =  asyncTrace() {
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