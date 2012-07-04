namespace SyncLib.Helpers

open System

module Seq =
    let tryTake (n : int) (s : _ seq) =
        let e = s.GetEnumerator ()
        let i = ref 0
        seq {
            while e.MoveNext () && !i < n do
                i := !i + 1
                yield e.Current
        }

module AsyncTrace = 
    
    
    type IAsyncTraceBuilder<'Info> =
        abstract member Info : 'Info option with get
        abstract member SetInfo : 'Info -> unit
        abstract member Capture : System.Collections.Generic.List<IAsyncTraceBuilder<'Info>> -> unit
    
    type BuilderList<'Info> = System.Collections.Generic.List<IAsyncTraceBuilder<'Info>>

    type AsyncTraceBuilder<'Info, 'T>(info:'Info option, myAsync:Async<'T>, list:BuilderList<'Info>) as x = 
        let mutable info = info
        let mutable internalList = list
        let builded = new BuilderList<'Info>()
        let syncList() = 
            // Sync List
            printfn "Sync %d items" (list|>Seq.length)
            let dataItems = 
                list 
                    |> Seq.filter (fun f -> f.Info.IsSome)
            
            let mutable data = None
            for dataItem in dataItems do
                let info = dataItem.Info
                if (info.IsSome) then
                    if (data.IsSome && obj.ReferenceEquals(data.Value, info.Value)) then 
                        failwith "multiple different data!"
                    data <- info
            if (data.IsSome) then
                for dataItem in list |> Seq.filter (fun f -> f.Info.IsNone) do
                    dataItem.SetInfo data.Value


        let buildTrace info async = 
            let b = new AsyncTraceBuilder<'Info, 'Y>(info, async, internalList)
            builded.Add(b)
            internalList.Add(b)
            b
            

        let bind (value:AsyncTraceBuilder<'Info, 'X>) (f:'X -> AsyncTraceBuilder<'Info, 'Y>) =
            value.Capture internalList
            syncList()
            
            let rec tracer =
                buildTrace 
                    info 
                    (async.Bind(
                        value.Async, 
                        (fun (t) -> 
                            let inner = f(t)
                            inner.SetInfo tracer.Info.Value
                            inner.Async)))
            //tracer.AddSubBuilder value
            //subBuilder.Add(value)
            tracer

        let delay (f:unit -> AsyncTraceBuilder<'Info, 'Y>) = 
            syncList()
            buildTrace info (async.Delay (fun () -> (f()).Async))

        let returnN t = 
            syncList()
            buildTrace info (async.Return(t))

        let returnFrom (t:AsyncTraceBuilder<'Info, _>) = 
            syncList()
            t.Capture internalList
            buildTrace info (async.ReturnFrom(t.Async))

        let combine (item1:AsyncTraceBuilder<'Info, _>) (item2:AsyncTraceBuilder<'Info, _>) = 
            syncList()
            item1.Capture internalList
            item2.Capture internalList
            buildTrace info (async.Combine(item1.Async, item2.Async))

        let forComp (sequence:seq<'X>) (work:'X -> AsyncTraceBuilder<'Info, unit>) = 
            syncList()
            buildTrace info (async.For(sequence, (fun t -> (work t).Async)))

        let tryFinally (item:AsyncTraceBuilder<'Info,_>) f = 
            syncList()
            item.Capture internalList
            buildTrace info (async.TryFinally(item.Async, f))
        
        let tryWith (item: AsyncTraceBuilder<'Info,_>) (exnHandler:exn ->  AsyncTraceBuilder<'Info,_>) = 
            syncList()
            item.Capture internalList
            buildTrace info (async.TryWith(item.Async, (fun xn -> (exnHandler xn).Async)))
        
        let usingComp (item) (doWork:_-> AsyncTraceBuilder<'Info,_>) = 
            syncList()
            buildTrace info (async.Using(item, (fun t -> (doWork t).Async)))

        let whileComp (item) (work: AsyncTraceBuilder<'Info,_>) = 
            syncList()
            work.Capture internalList
            buildTrace info (async.While(item, work.Async))
    
        let zeroComp () = buildTrace info (async.Zero())

        new() = AsyncTraceBuilder<'Info, 'T>(None, async { return Unchecked.defaultof<'T> }, new BuilderList<'Info>())

        interface IAsyncTraceBuilder<'Info> with
            member x.Info
                with get () = x.Info
            member x.SetInfo value = x.SetInfoSimple value
            member x.Capture list = x.Capture list

        member private x.Capture (list:BuilderList<'Info>) : unit = 
            
            if not (list.Contains(x)) then list.Add(x)
            for current in internalList do
                if not (list.Contains(current)) then 
                    list.Add(current)
                    current.Capture list
            
            internalList <- list
            for b in builded do 
                b.Capture(list)
                    
            printfn "Capture..."

        // AsyncTraceBuilder<'Info, 'T> * ('T -> AsyncTraceBuilder<'Info, 'U>) -> AsyncTraceBuilder<'Info, 'U>
        member x.Bind(value, f) = bind value f
        // (unit -> AsyncTraceBuilder<'Info,'T>) -> AsyncTraceBuilder<'Info,'T> 
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

        member x.Info 
            with get() : 'Info option = info
        member x.SetInfo (value:'Info) : unit = 
            info <- Some value
            for inner in internalList do
                inner.SetInfo value
        member x.SetInfoSimple (value:'Info) : unit = 
            info <- Some value

        member x.Async with get() : Async<'T> = myAsync

    let asyncTrace() = new AsyncTraceBuilder<_,_>()
    
    let traceInfo () : AsyncTraceBuilder<'a,_> =
        asyncTrace() {
            let b = asyncTrace() {return()}
            let! a = b // Small hack to get connected
            return 
                match b.Info with
                | None -> 
                    failwith "Please set the info value via builder"
                | Some v -> v : 'a 
        } 

    let convertFromAsync asy = new AsyncTraceBuilder<_,_>(None, asy, new BuilderList<_>())
    let convertToAsync (traceAsy:AsyncTraceBuilder<_,_>) = traceAsy.Async:Async<_>

    type TestType = int
    let testThisThing() = 
        let asy = 
            asyncTrace() {
                let! info = traceInfo()
                printfn "Found %d" info
            }   
        asy.SetInfo 20
        printfn "beforeStart"
        asy |> convertToAsync |> Async.RunSynchronously

    let anotherFun ()  = 
        asyncTrace() {
            let! info = traceInfo()
            let b = 
                async {
                    return 2
                }
            let! t = b |> convertFromAsync
            return info
        }


module Logger = 
    let logHelper ty (s : string) = Yaaf.Utils.Logging.Logger.WriteLine("{0}", ty, s)
    let log ty fmt = Printf.kprintf (logHelper ty) fmt
    let logVerb fmt = log System.Diagnostics.TraceEventType.Verbose fmt
    let logWarn fmt = log System.Diagnostics.TraceEventType.Warning fmt
    let logCrit fmt = log System.Diagnostics.TraceEventType.Critical fmt
    let logErr fmt = log System.Diagnostics.TraceEventType.Error fmt
    let logInfo fmt = log System.Diagnostics.TraceEventType.Information fmt     

module Event =
  let guard f (e:IEvent<'Del, 'Args>) = 
    let e = Event.map id e
    { new IEvent<'Args> with 
        member x.AddHandler(d) = e.AddHandler(d)
        member x.RemoveHandler(d) = e.RemoveHandler(d); f()
        member x.Subscribe(observer) = 
          let rm = e.Subscribe(observer) in f(); rm }

module Observable =
  let guard f (e:IObservable<'Args>) = 
    { new IObservable<'Args> with 
        member x.Subscribe(observer) = 
          let rm = e.Subscribe(observer) in f(); rm }

