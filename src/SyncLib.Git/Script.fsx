// Diese Datei ist ein Skript, das mit F# interaktiv ausgeführt werden kann.  
// Es kann zur Erkundung und zum Testen des Bibliotheksprojekts verwendet werden.
// Skriptdateien gehören nicht zum Projektbuild.
#I @"..\SyncLib\bin\Debug"
#I @"bin\Debug"
#r "SyncLib.dll"
#r "SyncLib.Git.dll"
//#load "Helpers.fs"
//#load "GitProcess.fs"
//#load "GitRepositoryFolder.fs"
//#load "GitBackendManager.fs"

open SyncLib
open SyncLib.Git

let backendManager = new GitBackendManager()
    
let manager =
    backendManager.CreateFolderManager(
        new ManagedFolderInfo(
            "SomeName", 
            "D:\\Documents",
            "git@localdevserver:mydata.git",
            "git",
            "",
            new System.Collections.Generic.Dictionary<_,_>()))
manager.ProgressChanged
    |> Event.add (fun p -> printfn "New Progress %s" (p.ToString()))

manager.SyncConflict
    |> Event.add 
        (fun conf -> 
            match conf with
            | SyncConflict.Unknown(s) -> printfn "Unknown Conflict: %s" s)

manager.SyncError
    |> Event.add
        (fun error -> printfn "Error: %s" (error.ToString()))

manager.SyncStateChanged
    |> Event.add
        (fun changed -> printfn "State changed: %s" (changed.ToString()))

//manager.StartService()
//Received Error Line 
//State changed: Idle
//Error: System.AggregateException: Mindestens ein Fehler ist aufgetreten. ---> SyncLib.Git.GitProcessFailed: Eine Ausnahme vom Typ "SyncLib.Git.GitProcessFailed" wurde ausgelöst.
//   bei Microsoft.FSharp.Core.Operators.Raise[T](Exception exn)
//   bei <StartupCode$SyncLib-Git>.$GitProcess.RunAsync@269-6.Invoke(EventArgs _arg1) in D:\Projects\Aktuell\synclib\src\SyncLib.Git\GitProcess.fs:Zeile 275.
//   bei Microsoft.FSharp.Control.AsyncBuilderImpl.args@720.Invoke(a a)
//   --- Ende der internen Ausnahmestapelüberwachung ---
//---> (Interne Ausnahme #0) SyncLib.Git.GitProcessFailed: Eine Ausnahme vom Typ "SyncLib.Git.GitProcessFailed" wurde ausgelöst.
//   bei Microsoft.FSharp.Core.Operators.Raise[T](Exception exn)
//   bei <StartupCode$SyncLib-Git>.$GitProcess.RunAsync@269-6.Invoke(EventArgs _arg1) in D:\Projects\Aktuell\synclib\src\SyncLib.Git\GitProcess.fs:Zeile 275.
//   bei Microsoft.FSharp.Control.AsyncBuilderImpl.args@720.Invoke(a a)<---
