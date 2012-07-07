// ----------------------------------------------------------------------------
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.
// ----------------------------------------------------------------------------

open Yaaf.SyncLib
open Yaaf.SyncLib.Git

// Creating a backendmanager (required once per backend)
let backendManager = new GitBackendManager() :> IBackendManager
   
// Create a manager for a specific folder
let manager =
    backendManager.CreateFolderManager(
        new ManagedFolderInfo(
            "SomeName", 
            "D:\\Documents",
            "git@localdevserver:mydata.git",
            "git",
            "",
            new System.Collections.Generic.Dictionary<_,_>()))

// Listen to the events
manager.ProgressChanged
    |> Event.add (fun p -> printfn "New Progress %s" (p.ToString()))

manager.SyncConflict
    |> Event.add 
        (fun conf -> 
            match conf with
            | SyncConflict.Unknown(s) -> printfn "Unknown Conflict: %s" s)

manager.SyncError
    |> Event.add
        (fun error -> 
            match error with
            | SshException(message, log) ->
                printfn "%s" message
                printfn ">> Stopped the service!"
                manager.StopService()
            | ToolProcessFailed(errorCode, cmd, output, errorOutput) -> printfn "Unknown Tool Error(%s): %d, %s, %s" cmd errorCode output errorOutput
            | _ -> printfn "Error: %s" (error.ToString()))

manager.SyncStateChanged
    |> Event.add
        (fun changed -> printfn "State changed: %s" (changed.ToString()))

// Start the service
manager.StartService()
printfn ">> Started, any key to exit the service"
System.Console.ReadLine() |> ignore

// Stop the service to no listen to the folder any longer (any running operations will be finished)
manager.StopService()

printfn ">> Stopped, any key to exit the program"

// You could start the service again if you want
System.Console.ReadLine() |> ignore