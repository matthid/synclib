// ----------------------------------------------------------------------------
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.
// ----------------------------------------------------------------------------
namespace Yaaf.SyncLib

open System
open System.Collections
open System.Collections.Generic
open System.Linq
open System.Reflection
open System.Threading
open System.IO

[<AutoOpen>]
module Helpers = 
    module Seq =
        let tryTake (n : int) (s : _ seq) =
            s 
                |> Seq.mapi (fun i t -> i < n, t)
                |> Seq.takeWhile (fun (shouldTake, t) -> shouldTake)
                |> Seq.map (fun (shouldTake, t) -> t)
        
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

    /// Allowed to Match against a substring
    let (|Contains|_|) (contain:string) (data:string) =   
        if (data.Contains(contain)) then Some() else None

    let (|ContainsAll|_|) (containings:string list) (data:string) = 
        if containings |> List.exists (fun contain -> not <| data.Contains(contain)) then
            None
        else Some()

    let (|StartsWith|_|) (start:string) (data:string) =   
        if (data.StartsWith(start)) then Some(data.Substring(start.Length)) else None

    let (|EndsWith|_|) (endString:string) (data:string) =   
        if (data.EndsWith(endString)) then Some(data.Substring(0, data.Length - endString.Length)) else None

    let (|Equals|_|) x y = if x = y then Some() else None

    let (|Integer|_|) (str: string) =
        let mutable intvalue = 0
        if System.Int32.TryParse(str, &intvalue) then Some(intvalue)
        else None

    let (|Float|_|) (str: string) =
        let mutable floatvalue = 0.0
        if System.Double.TryParse(str, &floatvalue) then Some(floatvalue)
        else None
    
    let (|ParseRegex|_|) regex str =
        let m = System.Text.RegularExpressions.Regex(regex).Match(str)
        if m.Success
        then Some (List.tail [ for x in m.Groups -> x.Value ])
        else None
