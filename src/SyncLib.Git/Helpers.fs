namespace SyncLib.Helpers

open System

//type AsyncTraceInfo<'a,'b> = {
//        Async : Async<'a>;
//        Info : 'b }
//module AsyncTrace = 
//    let bind value f = 
//        { Async = async.Bind(value.Async, (fun (t) -> (f(t,value.Info)).Async));
//          Info = value.Info }
//    let delay f = 
//        { Async = async.Delay (fun () -> (f()).Async);
//          Info = (f()).Info }
//    type AsyncTraceBuilder<'Trace>(traceObj) = 
//        member x.Bind(value, f) = bind value f
//        member x.Delay(f) = delay f
//
//    let asyncTrace = new AsyncTraceBuilder()
    
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

module Seq =
    let tryTake (n : int) (s : _ seq) =
        let e = s.GetEnumerator ()
        let i = ref 0
        seq {
            while e.MoveNext () && !i < n do
                i := !i + 1
                yield e.Current
        }