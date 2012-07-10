// ----------------------------------------------------------------------------
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.
// ----------------------------------------------------------------------------
namespace Yaaf.SyncLib

open System.Diagnostics
open Yaaf.AsyncTrace

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
                    RedirectStandardInput = true,
                    UseShellExecute = false,
                    WorkingDirectory = workingDir,
                    CreateNoWindow = true,
                    Arguments = arguments))

    let addReceiveEvent event f = 
        let c = new System.Collections.Generic.List<_>()
        let customExn = ref null
        event
            |> Event.add (fun (data:DataReceivedEventArgs) ->
                try
                    // When there is data and we have no user exception
                    if data.Data <> null && !customExn = null then
                        match f(data.Data) with                    
                        | Option.Some t -> c.Add(t)
                        | Option.None -> ()
                with exn ->
                    try
                        toolProcess.Kill()
                    with
                        // already finished
                        | :? System.InvalidOperationException -> ()
                    customExn := exn
                )
        c, customExn

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

    member x.Kill() = toolProcess.Kill()
    member x.RunAsync() = 
        asyncTrace() {
            let! (t:ITracer) = traceInfo()

            // Collect error stream
            let errorBuilder = ref (new System.Text.StringBuilder())
            toolProcess.ErrorDataReceived 
                |> Event.add (fun data ->
                    if (data.Data <> null) then
                        #if DEBUG
                        printfn "Error Line Received: %s" data.Data
                        #endif
                        t.logVerb "Received Error Line %s" data.Data
                        (!errorBuilder).AppendLine(data.Data) |> ignore)
            
            let outputBuilder = ref (new System.Text.StringBuilder())
            toolProcess.OutputDataReceived 
                |> Event.add (fun data ->
                    if (data.Data <> null) then
                        #if DEBUG
                        printfn "Line Received: %s" data.Data
                        #endif
                        t.logVerb "Received Line %s" data.Data
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
                    |> AsyncTrace.FromAsync
                    
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
            let c, customExn = addReceiveEvent toolProcess.OutputDataReceived  lineReceived
            do! x.RunAsync()
            if (!customExn <> null) then raise !customExn
            return c
        }

    member x.RunWithErrorOutputAsync(lineReceived, errorReceived) = 
        asyncTrace() {
            let c, customExn = addReceiveEvent toolProcess.ErrorDataReceived errorReceived
            let! output = x.RunWithOutputAsync(lineReceived)            
            if (!customExn <> null) then raise !customExn
            return output, c
        }
