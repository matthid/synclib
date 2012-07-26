
#r "lib/Yaaf.AsyncTrace.dll"
#r "lib/Yaaf.SyncLib.dll"

open Yaaf.SyncLib
open Yaaf.AsyncTrace
open System.IO
open System.Diagnostics
open System
let consoleListener =new ConsoleTraceListener()
consoleListener.Filter <- new EventTypeFilter(SourceLevels.All)
consoleListener.TraceOutputOptions <- TraceOptions.DateTime ||| TraceOptions.Callstack
let source = new TraceSource( "test" )
source.Switch.Level <- SourceLevels.Verbose

source.Listeners.Add(consoleListener)
let tracer = Logging.DefaultTracer source "test"
GitProcess.status "git" "/home/reddragon/mydata" |> AsyncTrace.SetTracer tracer |> Async.Ignore |> Async.RunSynchronously
