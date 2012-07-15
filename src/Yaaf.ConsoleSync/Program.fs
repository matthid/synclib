// ----------------------------------------------------------------------------
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.
// ----------------------------------------------------------------------------

open Yaaf.SyncLib
open Yaaf.SyncLib.Svn
// Scripting syntax
open Yaaf.SyncLib.Ui.Scripting

let myManagers = [
        Manager Git "Documents" "D:\\Documents" "git@localdevserver:mydata.git" 
        Manager Svn "ppp" "D:\\Test2" "https://subversion.assembla.com/svn/parallel-proggn" 
    ]


BackendTester.start(myManagers)
















//open Yaaf.SyncLib.PubsubImplementation
//
//let test = new PubsubClient("notifications.sparkleshare.org", 80)
//test.Error
//    |> Event.add (fun e -> printfn "Error: %A" e)
//
//test.ChannelMessage
//    |> Event.add (fun (c, m) -> printfn "Received: %s on %s" m c)
//test.Start()
//async {
//    let! nfo = test.Info()
//    printfn "Received Info: %A" nfo
//} |> Async.Start
////async {
////    do! Async.Sleep (2000 * 2)
////    let! nfo = test.Ping()
////    printfn "Received Ping: %O" nfo
////} |> Async.Start
//
////async {
////    do! seq {
////        for i in 1..5 do
////            yield async {
////                do! Async.Sleep (2000 * (i+1))
////                let! nfo = test.Ping()
////                printfn "Received Ping: %O" nfo
////            }
////        } |> Async.Parallel |> Async.Ignore
////} |> Async.Start
//test.Subscribe("test")
//test.Announce "test" "message"
//
//printfn ">> Started, any key to exit the services"
//System.Console.ReadLine() |> ignore