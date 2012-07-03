// Weitere Informationen zu F# unter "http://fsharp.net".

namespace SyncLib.Git

open System.Diagnostics
open System.IO
open SyncLib.Helpers

type BranchType = 
    | Local
    | Remote
    | All

type RemoteType =   
    | ListVerbose
    /// rename from to
    | Rename of string * string
    | Remove of string
    /// git remote add {0} {1}
    | Add of string * string

type GitArguments = 
    /// ls-remote --exit-code \"{0}\" {1}
    | Ls_remote of string * string
    /// status --porcelain
    | Status
    | Init
    | Branch of BranchType
    | Remote of RemoteType

module HandleGitArguments = 
    let toCommandLine args = 
        match args with
        | Ls_remote(s, branch) -> sprintf "ls-remote --exit-code \"%s\" %s" s branch
        | Status -> sprintf "status --porcelain"
        | Init -> sprintf "init"
        | Branch (types) -> 
            sprintf "branch%s" 
                (match types with
                | Local -> ""
                | BranchType.Remote -> " -r"
                | All -> " -a")

        | Remote (remoteType) ->
            sprintf "remote %s" 
                (match remoteType with 
                | ListVerbose -> "-v"
                | Rename(from, toName) -> sprintf "rename %s %s" from toName
                | Remove(name) -> sprintf "rm %s" name
                | Add(name, url) -> sprintf "add %s %s" name url)

    let possibleGitPaths = 
        [ "/usr/bin/git";
         "/usr/local/bin/git"; 
         "/opt/local/bin/git"; 
         "/usr/local/git/bin/git";
         "C:\\Program Files (x86)\\Git\\bin\\git.exe";
         "msysgit\\bin\\git.exe"; ]

    let locateGit = 
        let gitPath = ref null
        fun () -> 
                if (!gitPath = null) then 
                    gitPath := 
                        match 
                            possibleGitPaths
                                |> List.tryFind (fun path -> File.Exists(path)) with
                        | Some(foundPath) -> foundPath
                        | None -> "git"

                !gitPath


type GitStatusType = 
    | Added    = 0
    | Modified = 1
    | Deleted  = 2
    | Renamed  = 3
    | Copied   = 4
    | Updated  = 5
    | None     = 6
    | Unknown  = 7
            
type GitFileStatus = {
    Status : GitStatusType;
    Path : string }
type GitStatus =   { 
        File1 : GitFileStatus;
        File2 : GitFileStatus;
    }

type GitBranch = {
        IsSelected : bool;
        Name : string }

type GitRemoteType = 
    | Fetch
    | Push

type GitRemoteInfo = {
        Name : string;
        Url : string;
        Type : GitRemoteType; }
        
module HandleGitData = 
    
    let resolveSpecialChars (s:string) = 
        let codesToString codes = 
            let cds = codes |> List.rev |> List.toArray
            if cds.Length > 0 
            then System.Text.Encoding.UTF8.GetString (cds)
            else ""

        let rec processIt (current:System.String) i codes = 
            if (s.Length <= i) 
            then current + codesToString codes 
            else
               if (s.[i] = '\\' &&
                   s.Length - i > 3 &&
                   System.Char.IsNumber (s.[i + 1]) &&
                   System.Char.IsNumber (s.[i + 2]) &&
                   System.Char.IsNumber (s.[i + 3])) then
                   processIt current (i+4) (System.Convert.ToByte(s.Substring (i + 1, 3), 8) :: codes) 
               else
                   processIt (current + codesToString(codes) + s.[i].ToString()) (i+1) []
        try     
            processIt "" 0 []
        with
            // Invalid string (ie "\803")
            // So just ignore the conversion and return original string?
            // Or leave it unhandled
            | :? System.FormatException ->
                s
    
    let convertToResovedPath (s:string) = 
        if s.StartsWith("\"") then resolveSpecialChars (s.Substring(1, s.Length - 2))
        else s

    let convertToStatus c = 
        match c with
        | 'A' -> GitStatusType.Added
        | 'M' -> GitStatusType.Modified
        | 'D' -> GitStatusType.Deleted
        | 'R' -> GitStatusType.Renamed
        | 'C' -> GitStatusType.Copied
        | 'U' -> GitStatusType.Updated
        | ' ' -> GitStatusType.None
        | '?' -> GitStatusType.Unknown
        | _ -> failwith "invalid Status code"

    let convertArrowPath (p:string) = 
        let i = p.IndexOf(" -> ")
        if (i <> -1) then
            convertToResovedPath (p.Substring (0, i)),
            convertToResovedPath (p.Substring (i + 4))
        else
            let cp = convertToResovedPath p
            cp, cp

exception GitProcessFailed of string

type GitProcess(workingDir:string, gitArguments) =
    let gitArguments = gitArguments
    let gitProcess = 
        new Process(
            StartInfo =
                new ProcessStartInfo(
                    FileName = HandleGitArguments.locateGit(),
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    WorkingDirectory = workingDir,
                    CreateNoWindow = true,
                    Arguments = HandleGitArguments.toCommandLine gitArguments))

    member x.Dispose disposing = 
        if (disposing) then
            gitProcess.Dispose()

    override x.Finalize() = 
        x.Dispose(false)
    interface System.IDisposable with
        member x.Dispose() = 
            x.Dispose(true)
            System.GC.SuppressFinalize(x)


    member x.RunAsync(afterStarted) = 
        async {
            // subscribe Exit event
            gitProcess.EnableRaisingEvents <- true
            
            let exitEvent = 
                gitProcess.Exited 
                    |> Async.AwaitEvent
            
            // Collect error stream
            let builder = new System.Text.StringBuilder()
            gitProcess.ErrorDataReceived 
                |> Event.add (fun data ->
                    
                    printf "Received Error Line %s\n" data.Data
                    if data.Data <> null then builder.AppendLine(data.Data) |> ignore)

            let start = gitProcess.Start()
            gitProcess.BeginErrorReadLine()
            afterStarted()
            let! exit = exitEvent
            gitProcess.WaitForExit()
            gitProcess.CancelErrorRead()


            // Wait for exit event
            let exitCode = gitProcess.ExitCode
            if exitCode <> 0 then raise (GitProcessFailed (builder.ToString()))
        }
            
    member x.StandardInput
        with get() = 
            gitProcess.StandardInput
            

    member x.RunWithOutputAsync(lineReceived) = 
        async {
            
            let c = new System.Collections.Generic.List<_>()
            gitProcess.OutputDataReceived 
                |> Event.add (fun data ->
                    match lineReceived(data.Data) with
                    | Some t -> c.Add(t)
                    | None -> ()
                    )
            
            do! x.RunAsync(fun () ->
                gitProcess.BeginOutputReadLine())
            
            gitProcess.CancelOutputRead()
            return c
        }

    static member RunGitStatusAsync(location) = 
        async {
            use gitProc = new GitProcess(location, Status)
            return!
                gitProc.RunWithOutputAsync(fun l ->
                    if (System.String.IsNullOrEmpty(l)) then None
                    else
                        let p1, p2 = HandleGitData.convertArrowPath (l.Substring(3))
                        let s1 = HandleGitData.convertToStatus l.[0]
                        let s2 = HandleGitData.convertToStatus l.[1]
                        Some { 
                            File1 = { Status = s1; Path = p1}; 
                            File2 = { Status = s2; Path = p2}
                        }
                )
        }

    static member RunGitStatus(location) = 
        GitProcess.RunGitStatusAsync(location)
            |> Async.RunSynchronously

    static member RunGitLsRemoteAsync(location, uri, branch) = 
        async {
            use gitProc = new GitProcess(location, Ls_remote(uri, branch))
            let! output = gitProc.RunWithOutputAsync(fun l -> 
                if (System.String.IsNullOrEmpty(l)) then None
                else Some l
                )

            return output.[0].Substring(0,40)
        }

    static member RunGitLsRemote(location, uri, branch) = 
        GitProcess.RunGitLsRemoteAsync(location, uri, branch)
            |> Async.RunSynchronously
      

    static member RunGitInitAsync(location) = 
        async {
            use gitProc = new GitProcess(location, Init)
            do! gitProc.RunAsync(id)
            return ()
        }

    static member RunGitBranchAsync(location, types) = 
        async {
            use gitProc = new GitProcess(location, Branch(types))
            return!
                gitProc.RunWithOutputAsync(fun l ->
                    if (System.String.IsNullOrEmpty(l)) then None
                    else
                        Some {
                            IsSelected = (l.[0] = '*');
                            Name = l.Substring(2) 
                        }
                )
        }

    static member RunGitRemoteAsync(location, types) = 
        async {
            use gitProc = new GitProcess(location, Remote(types))
            return!
                gitProc.RunWithOutputAsync(fun l ->
                    if (System.String.IsNullOrEmpty(l)) then None
                    else
                        let items = 
                            l.Split([|' '|])
                                |> Seq.filter (fun e -> not (System.String.IsNullOrWhiteSpace e))
                                |> Seq.map (fun e -> e.Trim())
                                |> Seq.toArray
                        Some {
                            Name = items.[0];
                            Url = items.[1];
                            Type = 
                                if items.[2].Contains("fetch") 
                                then Fetch
                                else Push
                        }
                )
        }