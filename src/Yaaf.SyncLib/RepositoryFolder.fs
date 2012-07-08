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

type CommitMessageChangeType =
    | Added | Deleted | Renamed | Updated

type CommitMessageFile = {
    ChangeType : CommitMessageChangeType
    FilePath : string 
    FilePathRename : string }
    

/// A IManagedFolder implementation for repositories (git, svn ...)
[<AbstractClass>]
type RepositoryFolder(folder : ManagedFolderInfo) as x = 
    let syncConflict = new Event<SyncConflict>()
    let syncError = new Event<System.Exception>()
    let syncStateChanged = new Event<SyncState>()
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
            let rec loop i =
                let tracer = new DefaultStateTracer(sprintf "Processing (%d): " i) :> ITracer
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
                                do! work |> AsyncTrace.SetTracer tracer 
                    with 
                        | exn -> 
                            tracer.logErr "Error in Processing: %s" (exn.ToString())
                            syncError.Trigger exn
                    return! loop (i+1)
                }
            loop 0 
            )

    /// Requests a UpSync Operation
    member x.RequestSyncUp () = 
        processor.Post(DoSyncUp)

    /// Requests a DownSync Operation
    member x.RequestSyncDown () = 
        processor.Post(DoSyncDown)

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
type EmptyRepository(folder:ManagedFolderInfo) as x =  
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