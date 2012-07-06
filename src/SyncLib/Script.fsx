// Diese Datei ist ein Skript, das mit F# interaktiv ausgeführt werden kann.  
// Es kann zur Erkundung und zum Testen des Bibliotheksprojekts verwendet werden.
// Skriptdateien gehören nicht zum Projektbuild.


#I @"bin\Debug"
#r "SyncLib.dll"

open SyncLib
open SyncLib.Helpers.AsyncTrace
open SyncLib.Helpers
open System.Diagnostics

// Test for ChangeWatcher


let createWatcher folder = 
    let watcher = new IntelligentLocalWatcher(folder, (fun err -> printfn "Error: %s" (err.ToString())))
    watcher.Changed
        |> Event.add (fun a -> printfn "triggered")

let reduceTime span = 
    let event = new Event<unit>()
    event.Publish 
        |> Event.reduceTime span
        |> Event.add ( fun a -> printfn "reduceTimeTriggered" )
        
    event

let tester = reduceTime (System.TimeSpan.FromSeconds(5.0))