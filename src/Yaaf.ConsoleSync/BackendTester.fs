// ----------------------------------------------------------------------------
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.
// ----------------------------------------------------------------------------
module BackendTester

open Yaaf.SyncLib

open Yaaf.SyncLib.Git

// Create a manager for a specific folder
let addManagerEvents (manager:IManagedFolder) =
    let folder = manager.Folder
    // Listen to the events
    folder.ProgressChanged
        |> Event.add (fun p -> printfn "New Progress %s" (p.ToString()))

    folder.SyncConflict
        |> Event.add 
            (fun conf -> 
                match conf with
                | SyncConflict.MergeConflict(file) -> printfn "Solving conflict for file %s" file
                | SyncConflict.FileLocked(file) -> printfn "A file is locked! %s" file
                | SyncConflict.Unknown(s) -> printfn "Unknown Conflict: %s" s)

    folder.Error
        |> Event.add
            (fun (state, error) -> 
                match error with
                | SshAuthException(message) ->
                    printfn "%s" message
                    printfn ">> Stopped the service!"
                    printfn ">> open a console and execute \"%s\" and if you are asked type \"yes\"" "ssh.exe git@yourserver"
                    manager.StopService()
                | ToolProcessFailed(errorCode, cmd, output, errorOutput) -> printfn "Unknown Tool Error(%s): %d, %s, %s" cmd errorCode output errorOutput
                | _ -> printfn "Error: %s" (error.ToString()))

    folder.SyncStateChanged
        |> Event.add
            (fun changed -> printfn "State changed: %s" (changed.ToString()))

let start (myManagers:(ManagedFolderInfo*IManagedFolder) list) = 
    
    let managers = 
        myManagers |> List.map snd

    managers 
        |> List.iter addManagerEvents

    // Start the service
    for manager in managers do
        manager.StartService()
    printfn ">> Started, any key to exit the services"
    System.Console.ReadLine() |> ignore

    // Stop the service to no listen to the folder any longer (any running operations will be finished)
    for manager in managers do
        manager.StopService()

    printfn ">> Stopped, any key to exit the program"

    // You could start the service again if you want
    System.Console.ReadLine() |> ignore
