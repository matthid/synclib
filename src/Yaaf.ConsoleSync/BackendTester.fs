// ----------------------------------------------------------------------------
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.
// ----------------------------------------------------------------------------
module BackendTester

open Yaaf.SyncLib

open Yaaf.SyncLib.Git

// Create a manager for a specific folder
let createManager (backendManager:IBackendManager) name folder server =
    let manager =
        backendManager.CreateFolderManager(
            new ManagedFolderInfo(
                name, 
                folder,
                server,
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
                    printfn ">> open a console and execute \"%s\" and if you are asked type \"yes\"" "ssh.exe git@yourserver"
                    manager.StopService()
                | ToolProcessFailed(errorCode, cmd, output, errorOutput) -> printfn "Unknown Tool Error(%s): %d, %s, %s" cmd errorCode output errorOutput
                | _ -> printfn "Error: %s" (error.ToString()))

    manager.SyncStateChanged
        |> Event.add
            (fun changed -> printfn "State changed: %s" (changed.ToString()))


    manager