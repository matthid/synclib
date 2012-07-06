﻿// ----------------------------------------------------------------------------
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.
// ----------------------------------------------------------------------------
namespace Yaaf.SyncLib.Helpers

open System
open System.Collections
open System.Collections.Generic
open System.Diagnostics
open System.Linq
open System.Reflection
open System.Threading

module Seq =
    let tryTake (n : int) (s : _ seq) =
        let e = s.GetEnumerator ()
        let i = ref 0
        seq {
            while e.MoveNext () && !i < n do
                i := !i + 1
                yield e.Current
        }
        
type ITracer = 
    abstract member log : Diagnostics.TraceEventType ->Printf.StringFormat<'a, unit> -> 'a

/// Allows tracing of async workflows
module AsyncTrace = 
    type IAsyncTrace<'Info> = 
        abstract member Info : 'Info option with get,set

        abstract member Capture : System.Collections.Generic.List<IAsyncTrace<'Info>> -> unit

    type TraceList<'Info> = System.Collections.Generic.List<IAsyncTrace<'Info>>
    type AsyncTrace<'Info, 'T>(info:'Info option, execute:Async<'Info option * 'T>) = 
        let mutable info = info
        let mutable list = new TraceList<'Info>() 
        interface IAsyncTrace<'Info> with
            member x.Info 
                with get() = info
                and set(newValue) = info <- newValue 
            member x.Capture (newList:TraceList<'Info>) =
                if not (newList.Contains(x)) then newList.Add(x)
                for current in list do
                    if not (newList.Contains(current)) then 
                        newList.Add(current)
                        current.Capture newList
                                    
                list <- newList

                let dataItems = 
                    list 
                        |> Seq.filter (fun f -> f.Info.IsSome)
                            
                let mutable data = None
                for dataItem in dataItems do
                    let info = dataItem.Info
                    if (info.IsSome) then
                        if (data.IsSome && not (obj.ReferenceEquals(data.Value, info.Value))) then 
                            failwith "multiple different data!"
                        data <- info
                if (data.IsSome) then
                    for dataItem in list |> Seq.filter (fun f -> f.Info.IsNone) do
                        dataItem.Info <- Some data.Value

        member internal x.Async = execute
        member x.SetInfo value = info <- Some value
    

    type AsyncTraceBuilder<'Info, 'T>() = 
        let internalList = new TraceList<'Info>()

        let buildTrace async = 
            let b = new AsyncTrace<'Info, 'Y>(None, async)
            (b :> IAsyncTrace<'Info>).Capture internalList
            b
            

        let bind (value:AsyncTrace<'Info, 'X>) (f:'X -> AsyncTrace<'Info, 'Y>) =
            (value :> IAsyncTrace<'Info>).Capture internalList
            
            buildTrace 
                (async.Bind(
                    value.Async, 
                    (fun (info, t) -> 
                        let inner = f(t)
                        (inner:>IAsyncTrace<'Info>).Info <- (value:>IAsyncTrace<'Info>).Info
                        inner.Async)))

        let delay (f:unit -> AsyncTrace<'Info, 'Y>) = 
            buildTrace (async.Delay (fun () -> (f()).Async))

        let returnN t = 
            buildTrace (
                async {
                    let! d = async.Return(t)
                    return None, d
                })

        let returnFrom (t:AsyncTrace<'Info, _>) = 
            (t:> IAsyncTrace<'Info>).Capture internalList
            buildTrace (async.ReturnFrom(t.Async))

        let combine (item1:AsyncTrace<'Info, unit>) (item2:AsyncTrace<'Info, _>) = 
            (item1:> IAsyncTrace<'Info>).Capture internalList
            (item2:> IAsyncTrace<'Info>).Capture internalList
            buildTrace (
                async.Combine(
                    async {
                        let! d = item1.Async
                        
                        return()
                    }, item2.Async))

        let forComp (sequence:seq<'X>) (work:'X -> AsyncTrace<'Info, unit>) = 
            buildTrace (
                async { 
                    let! t = 
                        async.For
                            (sequence, 
                            (fun t -> 
                                async {
                                    do! (work t).Async |> Async.Ignore
                                    return ()
                                })) 
                    return None, t } )

        let tryFinally (item:AsyncTrace<'Info,_>) f = 
            (item:> IAsyncTrace<'Info>).Capture internalList
            buildTrace (async.TryFinally(item.Async, f))
        
        let tryWith (item: AsyncTrace<'Info,_>) (exnHandler:exn -> AsyncTrace<'Info,_>) = 
            (item:> IAsyncTrace<'Info>).Capture internalList
            buildTrace (async.TryWith(item.Async, (fun xn -> (exnHandler xn).Async)))
        
        let usingComp (item) (doWork:_-> AsyncTrace<'Info,_>) = 
            buildTrace (async.Using(item, (fun t -> (doWork t).Async)))

        let whileComp (item) (work: AsyncTrace<'Info,_>) = 
            (work:> IAsyncTrace<'Info>).Capture internalList
            buildTrace 
                (async {
                    do! (async.While(
                            item, 
                            async {
                                do! work.Async |> Async.Ignore
                                return ()
                            }))
                    return None, ()
                })

        let zeroComp () = 
            buildTrace 
                (async {
                    let! t = async.Zero()
                    return None, t
                })


        // AsyncTrace<'Info, 'T> * ('T -> AsyncTrace<'Info, 'U>) -> AsyncTrace<'Info, 'U>
        member x.Bind(value, f) = bind value f
        // (unit -> AsyncTrace<'Info,'T>) -> AsyncTrace<'Info,'T> 
        member x.Delay(f) = delay f
        member x.Return(t) = returnN t
        member x.ReturnFrom(t) =  returnFrom t
        member x.Combine(t1,t2) = combine t1 t2
        member x.For(t1,t2) = forComp t1 t2
        member x.TryFinally(t1,t2) = tryFinally t1 t2
        member x.TryWith(t1,t2) = tryWith t1 t2
        member x.Using(t1,t2) = usingComp t1 t2
        member x.While(t1,t2) = whileComp t1 t2
        member x.Zero() = zeroComp ()


    let asyncTrace() = new AsyncTraceBuilder<_,_>()
    
    let traceInfo () : AsyncTrace<'a,_> =
        asyncTrace() {
            let b = (asyncTrace() {return()})
            let! a = b // Small hack to get connected
            return 
                match (b:>IAsyncTrace<_>).Info with
                | None -> 
                    failwith "Please set the info value via builder"
                | Some v -> v : 'a 
        } 

    let convertFromAsync asy = 
        new AsyncTrace<_,_>(None, 
            async {
                let! d = asy
                return None, d
            })
    let convertToAsync (traceAsy:AsyncTrace<_,_>) = 
        async {
            let! info,d = traceAsy.Async
            return d
        }

    let Ignore (traceAsy:AsyncTrace<_,_>) =
        asyncTrace() {
            let! item = traceAsy
            return ()
        }

    type ITracer with
        member x.logVerb fmt = x.log System.Diagnostics.TraceEventType.Verbose fmt
        member x.logWarn fmt = x.log System.Diagnostics.TraceEventType.Warning fmt
        member x.logCrit fmt = x.log System.Diagnostics.TraceEventType.Critical fmt
        member x.logErr fmt =  x.log System.Diagnostics.TraceEventType.Error fmt
        member x.logInfo fmt = x.log System.Diagnostics.TraceEventType.Information fmt     
    let globalTrace = new TraceSource("global")
    /// Will just print it to the "global" trace
    type DefaultStateTracer(state:string) = 
        let activity = Guid.NewGuid()
        // TODO: Do something more sophisticated here like getting the calling assembly
        let logHelper ty (s : string) =  
            let oldId = Trace.CorrelationManager.ActivityId;
            try
                if (oldId <> activity) then Trace.CorrelationManager.ActivityId <- activity
                
                globalTrace.TraceEvent(ty, 0, s);
                globalTrace.Flush();
            finally 
                Trace.CorrelationManager.ActivityId <- oldId
        interface ITracer with 
            member x.log ty fmt = Printf.kprintf (logHelper ty) fmt  

module Event =
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
  let guard f (e:IObservable<'Args>) = 
    { new IObservable<'Args> with 
        member x.Subscribe(observer) = 
          let rm = e.Subscribe(observer) in f(); rm }
