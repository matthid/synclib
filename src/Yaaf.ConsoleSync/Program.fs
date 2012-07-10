// ----------------------------------------------------------------------------
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.
// ----------------------------------------------------------------------------

open GitTesting
open SvnTesting

open Yaaf.SyncLib
open Yaaf.SyncLib.Svn

let myManagers = [
        GitTesting.createManager "Documents" "D:\\Documents" "git@localdevserver:mydata.git" 
        SvnTesting.createManager "ppp" "D:\\Test2" "https://subversion.assembla.com/svn/parallel-proggn" 
    ]

// Start the service
for manager in myManagers do
    manager.StartService()
printfn ">> Started, any key to exit the services"
System.Console.ReadLine() |> ignore

// Stop the service to no listen to the folder any longer (any running operations will be finished)
for manager in myManagers do
    manager.StopService()

printfn ">> Stopped, any key to exit the program"

// You could start the service again if you want
System.Console.ReadLine() |> ignore