// Weitere Informationen zu F# unter "http://fsharp.net".


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

manager.StartService()
printfn ">> Started, any key to exit the service"
System.Console.ReadLine() |> ignore

manager.StopService()

printfn ">> Stopped, any key to exit the program"
System.Console.ReadLine() |> ignore