namespace SyncLib.Helpers

open System

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
