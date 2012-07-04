namespace SyncLib

open System.Diagnostics
open SyncLib.Helpers.Logger

/// Will be thrown if the process doesn't end with exitcode 0
/// The Data concluded is a tuple of exitCode, output, errorOutput
exception ToolProcessFailed of int * string * string

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
        async {
            // subscribe Exit event
            toolProcess.EnableRaisingEvents <- true
            
            let exitEvent = 
                toolProcess.Exited 
                    |> Async.AwaitEvent
            
            // Collect error stream
            let errorBuilder = new System.Text.StringBuilder()
            toolProcess.ErrorDataReceived 
                |> Event.add (fun data ->
                    logVerb "Received Error Line: %s\n" (if data.Data = null then "{NULL}" else data.Data)
                    if data.Data <> null then errorBuilder.AppendLine(data.Data) |> ignore)
            
            let outputBuilder = new System.Text.StringBuilder()
            toolProcess.OutputDataReceived 
                |> Event.add (fun data ->
                    logVerb "Received Data Line: %s\n" (if data.Data = null then "{NULL}" else data.Data)
                    if data.Data <> null then outputBuilder.AppendLine(data.Data) |> ignore)

            let start = toolProcess.Start()
            toolProcess.BeginErrorReadLine()
            toolProcess.BeginOutputReadLine()

            // Wait for the process to finish
            let! exit = exitEvent
            toolProcess.WaitForExit()
            toolProcess.CancelErrorRead()
            toolProcess.CancelOutputRead()


            // Check exitcode
            let exitCode = toolProcess.ExitCode
            if exitCode <> 0 then raise (ToolProcessFailed (exitCode, outputBuilder.ToString(), errorBuilder.ToString()))
        }
            
    member x.StandardInput
        with get() = 
            toolProcess.StandardInput
            

    member x.RunWithOutputAsync(lineReceived) = 
        async {
            
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
        async {
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
