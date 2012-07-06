// ----------------------------------------------------------------------------
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.
// ----------------------------------------------------------------------------


#I @"bin\Debug"
#r "Yaaf.SyncLib.dll"

open Yaaf.SyncLib
open Yaaf.SyncLib.Helpers.AsyncTrace
open Yaaf.SyncLib.Helpers
open System.Diagnostics

// Test for ChangeWatcher
let createWatcher folder = 
    let watcher = new IntelligentLocalWatcher(folder, (fun err -> printfn "Error: %s" (err.ToString())))
    watcher.Changed
        |> Event.add (fun a -> printfn "triggered")
    watcher

let reduceTime span = 
    let event = new Event<unit>()
    event.Publish 
        |> Event.reduceTime span
        |> Event.add ( fun a -> printfn "reduceTimeTriggered" )
        
    event

let tester = reduceTime (System.TimeSpan.FromSeconds(5.0))