﻿// ----------------------------------------------------------------------------
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.
// ----------------------------------------------------------------------------
namespace Yaaf.SyncLib

open System
open System.Linq
open System.Reflection
open System.Threading
open System.IO

/// Module for little helper functions
[<AutoOpen>]
module Helpers = 

    module Collections = 
        type Queue<'a>(xs:'a list,rxs:'a list) =
            new()=Queue<'a>([],[])
            static member Empty = new Queue<'a>()
            static member Init l = new Queue<'a>(l, [])
            member q.IsEmpty = (xs.IsEmpty) && (rxs.IsEmpty)
            member q.Enqueue(x) = Queue(xs,x::rxs)
            member q.Dequeue() =
                if q.IsEmpty then 
                    failwith "cannot dequeue from empty queue"
                else 
                    match xs with
                    | [] -> (Queue(List.rev rxs,[])).Dequeue()
                    | y::ys -> (Queue(ys, rxs)),y
    type queue<'a> = Collections.Queue<'a>

    /// Helps for interaction with sockets in an async way
    module SocketHelper =
        open System.Collections.Generic
        open System.Net
        open System.Net.Sockets
        /// Converts a array to a list of arraysegments
        let private toIList<'T> (data : 'T array) =
            let segment = new System.ArraySegment<'T>(data)
            let data = new List<System.ArraySegment<'T>>() :> IList<System.ArraySegment<'T>>
            data.Add(segment)
            data

        type Socket with
            /// Async Version of Accept
            member this.MyAcceptAsync() =
                Async.FromBeginEnd(this.BeginAccept, this.EndAccept)
                
            /// Async Version of Connect
            member this.MyConnectAsync(ipAddress : IPAddress, port : int) =
                Async.FromBeginEnd(ipAddress, port, (fun (a1,a2,a3,a4) -> this.BeginConnect((a1:IPAddress),a2,a3,a4)),this.EndConnect)
                
            /// Async Version of Connect
            member this.MyConnectAsync(host : string, port : int) =
                Async.FromBeginEnd(host, port, (fun (a1,a2,a3,a4) -> this.BeginConnect((a1:string),a2,a3,a4)),this.EndConnect)

            /// Async Version of Send
            member this.MySendAsync(data : byte array, flags : SocketFlags) =
                Async.FromBeginEnd(toIList data, flags, (fun (a1,a2,a3,a4) ->  this.BeginSend(a1,a2,a3,a4)), this.EndSend)
                
            /// Async Version of Receive
            member this.MyReceiveAsync(data : byte array, flags : SocketFlags) =
                Async.FromBeginEnd(toIList data, flags, (fun (a1,a2,a3,a4) -> this.BeginReceive(a1,a2,a3,a4)), this.EndReceive)
                
            /// Async Version of Receive
            member this.MyReceiveAsync(data : byte array, offset:int,size:int, flags : SocketFlags) =
                Async.FromBeginEnd((data, offset, size, flags), (fun ((d,o, s,f),a3,a4) -> this.BeginReceive(d,o,s,f,a3,a4)), this.EndReceive)

            /// Async Version of Disconnect
            member this.MyDisconnectAsync(reuseSocket) =
                Async.FromBeginEnd(reuseSocket, this.BeginDisconnect, this.EndDisconnect)
    
    module Seq =
        /// Returns the first n items of s. If there are fewer items then alls are returned.
        let tryTake (n : int) (s : _ seq) =
            s 
                |> Seq.mapi (fun i t -> i < n, t)
                |> Seq.takeWhile (fun (shouldTake, t) -> shouldTake)
                |> Seq.map (fun (shouldTake, t) -> t)
        
    module Event =
        /// Executes f just after adding the event-handler
        let guard f (e:IEvent<'Del, 'Args>) = 
            let e = Event.map id e
            { new IEvent<'Args> with 
                member x.AddHandler(d) = e.AddHandler(d); f()
                member x.RemoveHandler(d) = e.RemoveHandler(d)
                member x.Subscribe(observer) = 
                  let rm = e.Subscribe(observer) in f(); rm }
    
        /// This esures that every x minutes there is only 1 Event at maximum
        /// (lots of events will be reduced to 1 ... and this 1 will be fired when there was no event for x min)
        let reduceTime (span:System.TimeSpan) event = 
            let newEvent = new Event<_>()
            let eventId = ref 0
            event
                // Event.scan is not threadsafe
                //|> Event.scan (fun state args -> (fst state) + 1, args) (0,Unchecked.defaultof<_>)
                |> Event.add 
                    (fun (args) ->
                        // lock the two lines if you want to be thread safe 
                        // (note: events are not thread safe in general!)
                        let myId = !eventId + 1
                        eventId := myId
                
                        async { 
                            do! Async.Sleep(int (span.TotalMilliseconds))
                            if (myId = !eventId) then 
                                newEvent.Trigger args
                        } |> Async.Start)

            newEvent.Publish


    module Observable =
        /// Executes f just after subscribing
      let guard f (e:IObservable<'Args>) = 
        { new IObservable<'Args> with 
            member x.Subscribe(observer) = 
              let rm = e.Subscribe(observer) in f(); rm }

    /// Checks if contain is a substring of data
    let (|Contains|_|) (contain:string) (data:string) =   
        if (data.Contains(contain)) then Some() else None

    /// Checks if all containings are substrings of data
    let (|ContainsAll|_|) (containings:string list) (data:string) = 
        if containings |> List.forall (fun contain -> data.Contains(contain)) then
            Some()
        else None
    /// Checks if any containings are substrings of data
    let (|ContainsAny|_|) (containings:string list) (data:string) = 
        if containings |> List.exists (fun contain -> data.Contains(contain)) then
            Some()
        else None
    /// Checks if data startswith start
    let (|StartsWith|_|) (start:string) (data:string) =   
        if (data.StartsWith(start)) then Some(data.Substring(start.Length)) else None
    /// Checks if data endswith endString
    let (|EndsWith|_|) (endString:string) (data:string) =   
        if (data.EndsWith(endString)) then Some(data.Substring(0, data.Length - endString.Length)) else None
    /// Checks if the given items are equal
    let (|Equals|_|) x y = if x = y then Some() else None

    let inline (|EqualsAny|_|) x y =
        if x |> List.exists (fun item -> item = y) then Some() else None

    /// Checks if the given string is an integer and returns it if so
    let (|Integer|_|) (str: string) =
        let mutable intvalue = 0
        if System.Int32.TryParse(str, &intvalue) then Some(intvalue)
        else None
        
    /// Checks if the given string is a float and returns it if so
    let (|Float|_|) (str: string) =
        let mutable floatvalue = 0.0
        if System.Double.TryParse(str, &floatvalue) then Some(floatvalue)
        else None
    
    /// Checks if the given regex does match the string and returns all matches
    let (|ParseRegex|_|) regex str =
        let m = System.Text.RegularExpressions.Regex(regex).Match(str)
        if m.Success
        then Some (List.tail [ for x in m.Groups -> x.Value ])
        else None
