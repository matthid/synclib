// ----------------------------------------------------------------------------
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.
// ----------------------------------------------------------------------------
namespace Yaaf.SyncLib

open Yaaf.SyncLib.Helpers
open Yaaf.SyncLib.PubsubImplementation

/// Opt in Api to watch local changes, will trigger on any folder changes
type SimpleLocalChangeWatcher(folder : string, onError)  = 
    let changedEvent = new Event<System.IO.WatcherChangeTypes * string * string>()
    let watcher = new System.IO.FileSystemWatcher(folder, "*")
    do
        watcher.IncludeSubdirectories <- true
        watcher.EnableRaisingEvents <- true
        watcher.NotifyFilter <-
            System.IO.NotifyFilters.Attributes ||| System.IO.NotifyFilters.CreationTime ||| System.IO.NotifyFilters.DirectoryName
            ||| System.IO.NotifyFilters.FileName ||| System.IO.NotifyFilters.LastAccess ||| System.IO.NotifyFilters.LastWrite
            ||| System.IO.NotifyFilters.Security ||| System.IO.NotifyFilters.Size

        // Watch folder
        watcher.Changed
            |> Event.map (fun args -> args.ChangeType, args.FullPath, args.FullPath) 
            |> Event.merge (watcher.Created |> Event.map (fun args -> args.ChangeType, "", args.FullPath))
            |> Event.merge (watcher.Deleted |> Event.map (fun args -> args.ChangeType, args.FullPath, ""))
            |> Event.merge (watcher.Renamed |> Event.map (fun args -> args.ChangeType, args.OldFullPath, args.FullPath))
            |> Event.add (fun args -> changedEvent.Trigger args)
      
        watcher.Error |> Event.add (fun er -> onError(er.GetException()))

    /// The Changed event will be triggered when a change occured.
    [<CLIEvent>]
    member x.Changed = changedEvent.Publish

/// Helper type for the PubsubConnection Processor
type PubsubConnectionMessages = 
    | SetNewPubsub
    | Subscribe of string
    | Unsubscribe of string

/// Wrapper around PubsubClient which takes care of errors
type PubsubConnection(host, port) as x =  
    let channelAnnouncement = new Event<string*string>()
    let mutable pubsub = Unchecked.defaultof<_>
    let mutable subscriptions = []
    let setnewPubsub () = 
        let pub = new PubsubClient(host, port)
        pub.Error
            |> Event.add (fun e -> x.OnError(e))

        pub.ChannelMessage
            |> Event.add (fun a -> channelAnnouncement.Trigger a)
        pubsub <- pub
        pub.Start()
        subscriptions |> List.iter (fun c -> pub.Subscribe c)
    let subscribe c =   
        subscriptions <- c::subscriptions
        pubsub.Subscribe(c)

    let unsubscribe c =   
        subscriptions <- subscriptions |> List.filter (fun channel -> c <> channel) 
        pubsub.Unsubscribe(c)

    let processor = 
        MailboxProcessor.Start(fun inbox -> async {
            while true do
                let! msg = inbox.Receive()
                match msg with 
                | SetNewPubsub -> setnewPubsub()
                | Subscribe(c) -> subscribe c
                | Unsubscribe(c) -> unsubscribe c
                })

    do  processor.Post(SetNewPubsub)
    /// will be called on error
    member private x.OnError e = processor.Post(SetNewPubsub)
    /// will be triggered on a channel message
    [<CLIEvent>]
    member x.Announcement = channelAnnouncement.Publish
    /// gets a event for the given channel name
    member x.Item 
        with get(s) =
            x.Announcement
                |> Event.filter (fun (c, m) -> c = s)
                |> Event.map (fun (_, m) -> m)
    /// Subscribes to the given channel
    member x.Subscribe c = processor.Post(Subscribe(c))
    /// Unsubscibes to the given channel
    member x.Unsubscribe c = processor.Post(Unsubscribe(c))
    /// Lists the current subscriptions
    member x.Subscriptions with get () = subscriptions

    
/// Opt in Api to watch remote changes
type PubsubChangeWatcher(event:IEvent<_>) = 
    let changedEvent = new Event<unit>()    
    do
        event
            |> Event.add (fun m -> changedEvent.Trigger())

    /// The Changed event will be triggered when a change occured.
    [<CLIEvent>]
    member x.Changed = changedEvent.Publish

type RemoteConnectionType =
    | NoConnectionType
    | Pubsub of System.Uri * string
     
module RemoteConnectionManager = 
    let getPubsubConnection = 
        let keyFromUri (uri:System.Uri) = sprintf "%s_%d" uri.Host uri.Port
        let connections = new System.Collections.Generic.Dictionary<_,_>()
        (fun uri ->
            let key = keyFromUri uri
            lock connections (fun () ->
                match connections.TryGetValue(key) with
                | true, value -> value
                | false, _ ->
                    let con = new PubsubConnection(uri.Host, uri.Port)
                    connections.Add(key, con)
                    con))

    let getRemoteConnectionType (folder:ManagedFolderInfo) =
        match folder.Additional.TryGetValue("PubsubUrl") with
        | true, pubsubUri -> 
            match folder.Additional.TryGetValue("PubsubChannel") with
            | true, pubsubChannel -> Pubsub(new System.Uri(pubsubUri),pubsubChannel)
            | _ -> NoConnectionType
        | _ -> NoConnectionType
    
    let getRemoteChanged =
        let lockObj = new obj()

        (fun (folder:ManagedFolderInfo) ->
        match getRemoteConnectionType folder with
        | NoConnectionType -> None
        | Pubsub(uri, channel) -> 
            let con = getPubsubConnection uri
            // This is save because all is private in this module
            // And this is the only place with a Subscribe call
            lock lockObj (fun () ->
                if not (con.Subscriptions |> List.exists (fun s -> s = channel)) then
                     con.Subscribe(channel))
            con.[channel]
                |> Event.map (fun t -> ()) 
                |> Some
                )


