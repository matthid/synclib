// ----------------------------------------------------------------------------
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.
// ----------------------------------------------------------------------------
namespace Yaaf.SyncLib

open Yaaf.AsyncTrace

/// Action the RepositoryFolder-Processor should execute
type ProcessorMessage = 
    | DoSyncUp
    | DoSyncDown

type CommitMessageChangeType =
    | Added | Deleted | Renamed | Updated

type CommitMessageFile = {
    ChangeType : CommitMessageChangeType
    FilePath : string 
    FilePathRename : string }

/// Idicates how conflicts will get resolved
type ConflictStrategy = 
    /// Keep the local version and discard the server changes (they are still in history)
    | KeepLocal
    /// Rename the server version
    | RenameServer
    /// Rename the local version
    | RenameLocal

/// will be triggered when something with the connection is not right
exception ConnectionException of string

/// Notify about offline state (which should be an exception)
exception OfflineException

/// A IManagedFolder implementation for repositories (git, svn ...)
[<AbstractClass>]
type RepositoryFolder(folder : ManagedFolderInfo) as x = 
    let syncConflict = new Event<SyncConflict>()
    let syncError = new Event<System.Exception>()
    let syncStateChanged = new Event<SyncState>()
    let conflictStrategy = 
        match folder.Additional |> Dict.tryGetValue "ConflictStrategy" with
        | Some("KeepLocal") -> KeepLocal
        | Some("RenameServer") -> RenameServer
        | Some("RenameLocal") -> RenameLocal
        | Some(c) -> failwith (sprintf "unknown/invalid ConflictStrategy (%s)" c)
        | None -> RenameLocal
    let offlineRetryDelay = 
        match folder.Additional |> Dict.tryGetValue "OfflineRetryDelay" with
        | Some (Float f) -> f * 1000.0 |> int
        | Some (c) -> failwith (sprintf "unknown/invalid OfflineRetryDelay (%s)" c)
        | _ -> 5000
    let mutable isStarted = false
    
    let doTask syncState getTask = 
        asyncTrace() {
            let! (t:ITracer) = AsyncTrace.TraceInfo()
            try
                try
                    syncStateChanged.Trigger syncState
                    t.logVerb "starting task"
                    do! getTask()
                    t.logVerb "task finished without error"
                finally
                    syncStateChanged.Trigger SyncState.Idle
            with 
                | OfflineException -> 
                    t.logWarn "recognized an offline state"
                    syncStateChanged.Trigger SyncState.Offline
                    // Try again
                    if offlineRetryDelay <> 0 then
                        do! Async.Sleep offlineRetryDelay |> AsyncTrace.FromAsync
                        x.RequestSyncDown()
                | exn -> 
                    t.logWarn "task finished with an error: %O" exn
                    syncError.Trigger exn
        }

    let traceSource = Logging.MySource "Yaaf.SyncLib.Processing" folder.Name
    let globalTracer = Logging.DefaultTracer traceSource (sprintf "RepositoryFolder")
    do  globalTracer.logVerb "Created RepositoryFolder-Type: %s" (x.GetType().FullName)
       
    /// This will ensure that only one sync process is running at any time
    let processor = 
        MailboxProcessor<_>.Start(fun inbox -> 
            let rec loop i =
                async {
                    use tracer = globalTracer.childTracer traceSource (sprintf "Round %d" i)
                    try
                        tracer.logVerb "Waiting for messages"
                        // Get All messages (or wait for the first if non available)
                        let! allmsgs = 
                            seq { 
                                if (inbox.CurrentQueueLength = 0) then
                                    yield inbox.Receive()
                                else
                                    for i in 1 .. inbox.CurrentQueueLength do
                                        yield inbox.Receive() 
                            }   
                            |> Async.Parallel

                        tracer.logVerb "got %d messages" allmsgs.Length
                        if (isStarted) then
                            // Make sure SyncDowns are prefered over SyncUp
                            // And also make sure we do not spam our queue
                            for msg in
                                allmsgs |> Set.ofSeq  |> Seq.sort |> Seq.toList |> List.rev do
                                let work, name =
                                    match msg with
                                    | DoSyncUp -> 
                                        doTask SyncState.SyncUp (fun () -> x.StartSyncUp()), "syncUp"
                                    | DoSyncDown ->
                                        doTask SyncState.SyncDown (fun () -> x.StartSyncDown()), "syncDown"

                                tracer.logVerb "starting work %s" name
                                do! work |> AsyncTrace.SetTracer (tracer.childTracer traceSource name)
                               
                    with 
                        | exn -> 
                            tracer.logErr "Error in Processing: %s" (exn.ToString())
                            syncError.Trigger exn
                    return! loop (i+1)
                }
            loop 0 
            )
    do
        processor.Error
            |> Event.add 
                (fun e -> 
                    globalTracer.logCrit "Mailbox crashed: %s" (e.ToString())
                    syncError.Trigger e)

    /// The requested Conflictstrategy
    member x.ConflictStrategy = conflictStrategy

    /// Helper method to allow tracing in child classes
    member x.SetTrace work = 
        work |> AsyncTrace.SetTracer globalTracer

    /// Requests a UpSync Operation
    member x.RequestSyncUp () = 
        globalTracer.logVerb "SyncUp requested"
        processor.Post(DoSyncUp)

    /// Requests a DownSync Operation
    member x.RequestSyncDown () = 
        globalTracer.logVerb "SyncDown requested"
        processor.Post(DoSyncDown)

    /// Generates a unified commit message for the given files
    member x.GenerateCommitMessage (files:CommitMessageFile seq) = 
        let commitMessage =
            files
                |> Seq.filter (fun f -> f.ChangeType <> CommitMessageChangeType.Updated || not (f.FilePath.EndsWith(".empty")))
                |> Seq.map (fun f -> 
                                if f.FilePath.EndsWith(".empty") then
                                    { f with FilePath = f.FilePath.Substring(0, 6) }
                                else f)
                |> Seq.collect 
                    (fun f ->
                        seq {
                            match f.ChangeType with
                            | CommitMessageChangeType.Added -> yield sprintf "+ '%s'" f.FilePath
                            | CommitMessageChangeType.Updated -> yield sprintf "/ '%s'" f.FilePath
                            | CommitMessageChangeType.Deleted -> yield sprintf "- '%s'" f.FilePath
                            | CommitMessageChangeType.Renamed -> 
                                yield sprintf "- '%s'" f.FilePath
                                yield sprintf "+ '%s'" f.FilePathRename
                        })
                |> Seq.tryTake 20
                |> Seq.fold (fun state item -> sprintf "%s\n%s" state item) ""

        (commitMessage + 
            if (files |> Seq.length > 20) 
            then "..."
            else "").TrimEnd()
        
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
type EmptyRepository(folder:ManagedFolderInfo) =  
    inherit RepositoryFolder(folder)
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