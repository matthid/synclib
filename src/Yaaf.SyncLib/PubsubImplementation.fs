// ----------------------------------------------------------------------------
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.
// ----------------------------------------------------------------------------
namespace Yaaf.SyncLib

/// Based on
/// https://github.com/travisghansen/fanout/blob/master/README
module PubsubImplementation =
    open Yaaf.SyncLib.Helpers.SocketHelper
    open System.Net
    open System.Net.Sockets
    open System.Threading.Tasks
    open Microsoft.FSharp.Control
    
    open Yaaf.AsyncTrace

    /// A internal message for the PubsubClient - Processor
    type internal PubsubProcessorMsg = 
        /// Send a ping message
        | Ping of AsyncReplyChannel<int>
        /// Send a info message
        | Info of AsyncReplyChannel<System.Collections.Generic.IDictionary<string,string>>
        /// Send a subscribe message
        | Subscribe of string
        /// Send a unsubscribe message
        | Unsubscribe of string
        /// Send a announce message
        | Announce of string * string
        /// Notify the processor on a received message
        | ReceivedMessage of string

    type internal PubsubProcessData = {
        Queue : obj Queue
        SubscribedChannels : string list
        InfoData : (string * string) list }
        with 
            static member Empty = {
                                    Queue = Queue.Empty
                                    SubscribedChannels = list.Empty
                                    InfoData = list.Empty }

    /// A simple Pubsub client
    type PubsubClient (host:string, port:int) =
        let channelMessageReceived = new Event<string*string>()
        let errorEvent = new Event<exn>()
        let checkStr paramName illegal s = 
            match s with
            | ContainsAny illegal -> invalidArg paramName (sprintf "invalid %s" paramName)
            | _ -> s
        let checkedChannelName channel = checkStr "channel" [ "\n"; "!"; " " ] channel
        let checkedMessage message = checkStr "message" [ "\n" ] message
        
        let traceSource = Logging.MySource "Yaaf.SyncLib.PubsubImplementation.PubsubClient" (sprintf "%s_%d" host port)
        let globalTracer = Logging.DefaultTracer traceSource (sprintf "Pubsub")
        do  globalTracer.logVerb "Created PubsubClientClient: %s:%d" host port
       
        /// The socket to the server
        let client = async {
                let socket = 
                    new Socket(
                        AddressFamily.InterNetwork, 
                        SocketType.Stream, 
                        ProtocolType.Tcp)
                do! socket.MyConnectAsync(host, port)
                return socket } |> Async.StartAsTask

        /// The networkstream of the server
        let networkStream = async {
                let! client = client |> Async.AwaitTask
                return new NetworkStream(client) } |> Async.StartAsTask
        
        /// The Global processor manages the sending and receiving of data
        let globalProcessor = 
            new MailboxProcessor<_>(fun inbox -> async {
                let tracer = globalTracer.childTracer traceSource (sprintf "GlobalProcessor")
                let! client = client |> Async.AwaitTask
                let! stream = networkStream |> Async.AwaitTask
                let writer = new AsyncStreamWriter(stream, System.Text.Encoding.UTF8, "\n")

                /// writes a given line to the network and logs the event
                let writeLine s = async {
                    tracer.logVerb "Sending message: %s" s
                    do! writer.WriteLine(s) 
                    }
                /// Take the next item of the queue, respond and return the new queue
                let respondQueue (queue:Queue<obj>) (m:'a) = 
                    let v,q = queue |> Queue.dequeue
                    (v:?>AsyncReplyChannel<'a>).Reply(m)
                    q


                // Wait for connect message first
                do! inbox.Scan
                        (fun m ->
                            match m with
                            | ReceivedMessage(line) ->
                                match line with
                                | StartsWith "debug!connected" rest -> 
                                    tracer.logVerb "Got connect message"
                                    Some(async{return()})
                                | _ -> None
                            | _ -> None) 

                /// Handle a received message and calculate the new queue and subscriptionlist
                let receiveMessage (line:string) (data:PubsubProcessData) =

                    // Check if we received a info message
                    let bangIndex = line.IndexOf('!')
                    let colonIndex = line.IndexOf(':')
                    let data =
                        if (bangIndex <> -1 || colonIndex = -1) && data.InfoData.IsEmpty |> not then 
                            // No info message, so finish current info message
                            { data with InfoData = []; Queue = (respondQueue data.Queue (dict data.InfoData)) }
                        else data

                    match bangIndex with
                    | -1 ->
                        match line with 
                        | Integer timestamp -> // ping reply
                            { data with Queue = (respondQueue data.Queue timestamp) }
                        | _ -> 
                            match colonIndex with
                            | -1 -> 
                                tracer.logErr "Unknown line: \"%s\" " line
                                data
//                            | StartsWith "uptime: " rest ->
//                                // 15d 4h 32m 50s
//                                let parsed = 
//                                    System.TimeSpan.ParseExact(rest, @"d\d\ h\h\ m\m\ s\s", System.Globalization.CultureInfo.InvariantCulture)
//                                (respondQueue queue parsed), subscribed
                            | _ as sep ->
                                let typeInfo = line.Substring(0, sep), line.Substring(sep + 2)
                                { data with InfoData = typeInfo :: data.InfoData }
                    | _ ->
                        let channel, message = 
                            line.Substring(0, bangIndex), line.Substring(bangIndex + 1)
                        match channel with
                        // Ignore debug message, it is logged though
                        | Equals "debug" -> data
                        | EqualsAny ("all" :: data.SubscribedChannels) -> 
                            channelMessageReceived.Trigger(channel, message)
                            data
                        | _ ->
                            tracer.logErr "Unknown message: \"%s\" on channel \"%s\"" message channel
                            data

                // NOTE: This is required or we don't get the answer of the first command
                // I don't saw this documented anywhere so I guess thats either the protocol (to start a message with "\n")
                // Or the server is not implemented properly
                do! writer.Write "\n"

                /// Starts the processing
                let rec loop (data:PubsubProcessData) = async {
                    let! msg = inbox.Receive()
                    match msg with 
                    | Ping(replyMessage) -> 
                        do! writeLine (sprintf "ping")
                        return! loop { data with Queue = (data.Queue |> Queue.enqueue (replyMessage:>obj)) }
                    | Info(replyMessage) -> 
                        do! writeLine (sprintf "info")
                        return! loop { data with Queue = (data.Queue |> Queue.enqueue (replyMessage:>obj)) }
                    | Subscribe(channel) ->
                        do! writeLine (sprintf "subscribe %s" channel)
                        return! loop { data with SubscribedChannels = channel :: data.SubscribedChannels }
                    | Unsubscribe(channel) ->
                        do! writeLine (sprintf "unsubscribe %s" channel)
                        return! loop { data with SubscribedChannels = data.SubscribedChannels |> List.filter (fun i -> i <> channel) }
                    | Announce(channel, message) ->
                        do! writeLine (sprintf "announce %s %s" channel message)
                        return! loop data
                    | ReceivedMessage(line) ->
                        tracer.logVerb "Received Line: %s" line
                        return! loop (receiveMessage line data)
                    tracer.logCrit "Mailbox stopped on \"%O\" " msg
                    return! loop data }

                return! loop PubsubProcessData.Empty})

        do 
            /// Attach the error event (in case of unhandled exceptions in MailboxProcessor
            globalProcessor.Error 
                |> Event.add 
                    (fun e -> 
                        globalTracer.logCrit "ReceiveMailbox crashed PubsubClientClient: %O" e
                        errorEvent.Trigger e)

        /// The Receiving task
        let receiveTask = async {
                try
                    let! stream = networkStream |> Async.AwaitTask
                    let readutf8 = new AsyncStreamReader(stream, System.Text.Encoding.UTF8)
                    while true do
                        globalTracer.logVerb "Waiting for message"
                        let! line = readutf8.ReadLine()
                        globalProcessor.Post(ReceivedMessage(line))
                    return () 
                with 
                    exn -> 
                        globalTracer.logCrit "ReceiveTask crashed: %O" exn
                        errorEvent.Trigger exn } |> Async.StartAsTask
        
        /// Starts the message processing (or triggers the Error event in case of Exceptions)
        member x.Start () = 
            globalProcessor.Start()

        /// Will be triggered on any kind of error of the Client
        [<CLIEvent>]
        member x.Error = errorEvent.Publish

        /// Will be triggered on any channel message
        [<CLIEvent>]
        member x.ChannelMessage = channelMessageReceived.Publish

        /// Subscribes to the given channel
        member x.Subscribe channel = 
            let channel = checkedChannelName channel
            globalProcessor.Post(Subscribe(channel))

        /// Unsubscribes from the given channel
        member x.Unsubscribe channel = 
            let channel = checkedChannelName channel
            globalProcessor.Post(Unsubscribe(channel))

        /// Announces a message to the given channel
        member x.Announce channel message =
            let channel = checkedChannelName channel
            let message = checkedMessage message
            globalProcessor.Post(Announce(channel, message))

        /// sends a ping message and returns the result (timestamp of server)
        member x.Ping () = globalProcessor.PostAndAsyncReply(fun channel -> Ping(channel))

        /// sends a info message and returns the result
        /// NOTE: it is impossible to figure out the end of a info stream, 
        /// and this implementation buffers all infos until another message arrives
        /// (to be sure there are no more infos). 
        /// So to be sure to get an answer send a ping just after sending the info request.
        member x.Info () =
            globalProcessor.PostAndAsyncReply(fun channel -> Info(channel))



