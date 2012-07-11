// ----------------------------------------------------------------------------
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.
// ----------------------------------------------------------------------------


#I @"bin\Debug"
#r "Yaaf.SyncLib.dll"
#r "Yaaf.AsyncTrace.dll"

open Yaaf.SyncLib.PubsubImplementation
open Yaaf.AsyncTrace
open Yaaf.SyncLib.Helpers
open System.Diagnostics

let test = new PubsubClient("notifications.sparkleshare.org", 80)
test.Error
    |> Event.add (fun e -> printfn "Error: %A" e)

test.ChannelMessage
    |> Event.add (fun (c, m) -> printfn "Received: %s on %s" m c)

// test.Start()
//test.Subscribe("Test")


//// Interactive Test for ReduceTime
//let reduceTime span = 
//    let event = new Event<unit>()
//    event.Publish 
//        |> Event.reduceTime span
//        |> Event.add ( fun a -> printfn "reduceTimeTriggered" )
//        
//    event
//
//let tester = reduceTime (System.TimeSpan.FromSeconds(5.0))
//
