namespace SyncLib

open System.Diagnostics
open SyncLib.Helpers
open SyncLib.Helpers.AsyncTrace

/// Will be thrown if the process doesn't end with exitcode 0
/// The Data concluded is a tuple of exitCode, commandLine, output, errorOutput
exception ToolProcessFailed of int * string * string * string

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

            // subscribe Exit event
            
            
            
            
            // Collect error stream
            let errorBuilder = ref (new System.Text.StringBuilder())
            toolProcess.ErrorDataReceived 
                |> Event.add (fun data ->
                    if (data.Data = null) then
                        t.logVerb "Received Error Line: {NULL}\n"
                    else
                        t.logVerb "Received Error Line: %s\n" data.Data
                        (!errorBuilder).AppendLine(data.Data) |> ignore)
            
            let outputBuilder = ref (new System.Text.StringBuilder())
            toolProcess.OutputDataReceived 
                |> Event.add (fun data ->
                    if (data.Data = null) then
                        t.logVerb "Received Data Line: {NULL}\n"
                    else
                        t.logVerb "Received Data Line: %s\n" data.Data
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
                    
            toolProcess.CancelErrorRead()
            toolProcess.CancelOutputRead()


            // Check exitcode
            let exitCode = toolProcess.ExitCode
            if exitCode <> 0 then 
                raise (ToolProcessFailed (exitCode, sprintf "%s/%s %s" workingDir processFile arguments,  (!outputBuilder).ToString(), (!errorBuilder).ToString()))
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
