// ----------------------------------------------------------------------------
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.
// ----------------------------------------------------------------------------

open GitTesting
open SvnTesting

open Yaaf.SyncLib
open Yaaf.SyncLib.Svn



open Yaaf.SyncLib.PubsubImplementation

let test = new PubsubClient("notifications.sparkleshare.org", 80)
test.Error
    |> Event.add (fun e -> printfn "Error: %A" e)

test.ChannelMessage
    |> Event.add (fun (c, m) -> printfn "Received: %s on %s" m c)
test.Start()
async {
    let! nfo = test.Info()
    printfn "Received Info: %A" nfo
} |> Async.Start
async {
    do! Async.Sleep (2000 * 2)
    let! nfo = test.Ping()
    printfn "Received Ping: %O" nfo
} |> Async.Start

//async {
//    do! seq {
//        for i in 1..5 do
//            yield async {
//                do! Async.Sleep (2000 * (i+1))
//                let! nfo = test.Ping()
//                printfn "Received Ping: %O" nfo
//            }
//        } |> Async.Parallel |> Async.Ignore
//} |> Async.Start
test.Subscribe("test")
test.Announce "test" "message"

printfn ">> Started, any key to exit the services"
System.Console.ReadLine() |> ignore

//
//
//let myManagers = [
//        GitTesting.createManager "Documents" "D:\\Documents" "git@localdevserver:mydata.git" 
//        SvnTesting.createManager "ppp" "D:\\Test2" "https://subversion.assembla.com/svn/parallel-proggn" 
//    ]
//
//// Start the service
//for manager in myManagers do
//    manager.StartService()
//printfn ">> Started, any key to exit the services"
//System.Console.ReadLine() |> ignore
//
//// Stop the service to no listen to the folder any longer (any running operations will be finished)
//for manager in myManagers do
//    manager.StopService()
//
//printfn ">> Stopped, any key to exit the program"
//
//// You could start the service again if you want
//System.Console.ReadLine() |> ignore