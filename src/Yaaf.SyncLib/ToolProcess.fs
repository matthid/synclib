// ----------------------------------------------------------------------------
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.
// ----------------------------------------------------------------------------
namespace Yaaf.SyncLib

open System.Diagnostics
open Yaaf.SyncLib.Helpers
open Yaaf.SyncLib.Helpers.AsyncTrace

/// Will be thrown if the process doesn't end with exitcode 0.
/// The Data contained is a tuple of exitCode, commandLine, output, errorOutput.
exception ToolProcessFailed of int * string * string * string

/// A simple wrapper for asyncronus process starting
type ToolProcess(processFile:string, workingDir:string, arguments:string) =
    let toolProcess = 
        new Process(
            StartInfo =
                new ProcessStartInfo(
                    FileName = processFile,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    WorkingDirectory = workingDir,
                    CreateNoWindow = true,
                    Arguments = arguments))
    do 
        toolProcess.EnableRaisingEvents <- false

    member x.Dispose disposing = 
        if (disposing) then
            toolProcess.Dispose()

    override x.Finalize() = 
        x.Dispose(false)

    interface System.IDisposable with
        member x.Dispose() = 
            x.Dispose(true)
            System.GC.SuppressFinalize(x)


    member x.RunAsync() = 
        asyncTrace() {
            let! (t:ITracer) = AsyncTrace.traceInfo()

            // Collect error stream
            let errorBuilder = ref (new System.Text.StringBuilder())
            toolProcess.ErrorDataReceived 
                |> Event.add (fun data ->
                    if (data.Data <> null) then
                        (!errorBuilder).AppendLine(data.Data) |> ignore)
            
            let outputBuilder = ref (new System.Text.StringBuilder())
            toolProcess.OutputDataReceived 
                |> Event.add (fun data ->
                    if (data.Data <> null) then
                        (!outputBuilder).AppendLine(data.Data) |> ignore)

            toolProcess.Start() |> ignore
            toolProcess.BeginErrorReadLine()
            toolProcess.BeginOutputReadLine()
            
            // Wait for the process to finish
            let! exitEvent = 
                toolProcess.Exited 
                    |> Event.guard 
                        (fun () ->
                            toolProcess.EnableRaisingEvents <- true)
                    |> Async.AwaitEvent
                    |> AsyncTrace.convertFromAsync
                    
            toolProcess.WaitForExit()
            let exitCode = toolProcess.ExitCode

            // Should run only 1 time
            toolProcess.Close()
            toolProcess.Dispose()

            // Check exitcode
            if exitCode <> 0 then 
                let failedCmd = sprintf "%s> \"%s\" %s" workingDir processFile arguments
                let output = (!outputBuilder).ToString()
                let error = (!errorBuilder).ToString()
                t.logErr "ToolProcess failed!\n\tCommand Line (exited with %d): %s\n\tOutput: %s\n\tError: %s" exitCode failedCmd output error
                raise (ToolProcessFailed (exitCode, failedCmd, output, error))
        }
            
    member x.StandardInput
        with get() = 
            toolProcess.StandardInput
            

    member x.RunWithOutputAsync(lineReceived) = 
        asyncTrace() {
            
            let c = new System.Collections.Generic.List<_>()
            toolProcess.OutputDataReceived 
                |> Event.add (fun data ->
                    match lineReceived(data.Data) with                    
                    | Option.Some t -> c.Add(t)
                    | Option.None -> ()
                    )
            
            do! x.RunAsync()
            return c
        }

    member x.RunWithErrorOutputAsync(lineReceived, errorReceived) = 
        asyncTrace() {
            let c = new System.Collections.Generic.List<_>()
            toolProcess.ErrorDataReceived 
                |> Event.add (fun data ->
                    match errorReceived(data.Data) with
                    | Option.Some t -> c.Add(t)
                    | Option.None -> ()
                    )
            
            let! output = x.RunWithOutputAsync(lineReceived)
            return output, c
        }
