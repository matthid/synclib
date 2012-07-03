// Weitere Informationen zu F# unter "http://fsharp.net".

namespace SyncLib.Git

open System.Diagnostics
open System.IO
open SyncLib.Helpers
open SyncLib.Helpers.Logger

type BranchType = 
    | Local
    | Remote
    | All

type RemoteType =   
    | ListVerbose
    /// git remote rename from to
    | Rename of string * string
    | Remove of string
    /// git remote add {0} {1}
    | Add of string * string
type ConflictFileType = 
    | Ours
    | Theirs
    
type CheckoutType = 
    | Conflict of ConflictFileType * string list
    | Branch of string
type RebaseType =   
    | Continue
    | Abort
    | Skip
    /// git rebase from to
    | Start of string * string
type GitAddType =   
    | NoOptions
    | Update
    | All
type GitArguments = 
    /// ls-remote --exit-code \"{0}\" {1}
    | Ls_remote of string * string
    /// status --porcelain
    | Status
    | Init
    | Branch of BranchType
    | Remote of RemoteType
    /// fetch --progress \"{0}\" {1}
    | Fetch of string * string
    /// push --progress \"{0}\" {1}
    | Push of string * string
    | Rebase of RebaseType
    | Add of GitAddType * string list
    /// commit -m \"{0}\"
    | Commit of string
    | Checkout of CheckoutType

module HandleGitArguments = 
    let escapePath p = 
        p

    let parseFiles = List.fold (fun state item -> sprintf "%s \"%s\"" state (escapePath item)) "" 
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
                | BranchType.All -> " -a")

        | Remote (remoteType) ->
            sprintf "remote %s" 
                (match remoteType with 
                | ListVerbose -> "-v"
                | Rename(from, toName) -> sprintf "rename %s %s" from toName
                | Remove(name) -> sprintf "rm %s" name
                | RemoteType.Add(name, url) -> sprintf "add %s %s" name url)

        | Fetch(url, branch) -> sprintf "fetch --progress \"%s\" %s" url branch
        | Push(url, branch) -> sprintf "push --progress \"%s\" %s" url branch

        | Rebase (rebaseType) ->
            sprintf "rebase %s" 
                (match rebaseType with 
                | Continue -> "--continue"
                | Abort -> "--abort"
                | Skip -> "--skip"
                | Start(from, toName) -> sprintf "%s %s" from toName)

        | Add (addType, files) ->
            sprintf "add %s --%s"
                (match addType with
                | NoOptions -> ""
                | Update -> "-u"
                | GitAddType.All -> "-A")
                (files |> parseFiles)
        | Commit (message) -> sprintf "commit -m \"%s\"" message
        | Checkout (checkoutType) ->
            sprintf "checkout %s"
                (match checkoutType with
                | Conflict(confType, files) ->
                    sprintf "%s --%s"
                        (match confType with
                        | Ours -> "--ours"
                        | Theirs -> "--theirs")
                        (files |> parseFiles)
                | CheckoutType.Branch(name) -> name)
                    

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
                        (match 
                            possibleGitPaths
                                |> List.tryFind (fun path -> File.Exists(path)) with
                        | Option.Some(foundPath) -> foundPath
                        | Option.None -> "git")

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
        Local : GitFileStatus;
        Server : GitFileStatus;
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
                    logVerb "Received Error Line: %s\n" (if data.Data = null then "{NULL}" else data.Data)
                    if data.Data <> null then builder.AppendLine(data.Data) |> ignore)

            let start = gitProcess.Start()
            gitProcess.BeginErrorReadLine()
            afterStarted()
            let! exit = exitEvent
            gitProcess.WaitForExit()
            gitProcess.CancelErrorRead()


            // Wait for exit event
            let exitCode = gitProcess.ExitCode
            // TODO: Add normal output to exception 
            // (this would also make the afterStarted function redundand)
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
                    logVerb "Received Data Line: %s\n" (if data.Data = null then "{NULL}" else data.Data)
                    match lineReceived(data.Data) with                    
                    | Option.Some t -> c.Add(t)
                    | Option.None -> ()
                    )
            
            do! x.RunAsync(fun () ->
                gitProcess.BeginOutputReadLine())
            
            gitProcess.CancelOutputRead()
            return c
        }

    member x.RunWithErrorOutputAsync(lineReceived, errorReceived) = 
        async {
            let c = new System.Collections.Generic.List<_>()
            gitProcess.ErrorDataReceived 
                |> Event.add (fun data ->
                    match errorReceived(data.Data) with
                    | Option.Some t -> c.Add(t)
                    | Option.None -> ()
                    )
            
            let! output = x.RunWithOutputAsync(lineReceived)
            return output, c
        }

    static member RunGitStatusAsync(location) = 
        async {
            use gitProc = new GitProcess(location, Status)
            return!
                gitProc.RunWithOutputAsync(fun l ->
                    if (System.String.IsNullOrEmpty(l)) then Option.None
                    else
                        let p1, p2 = HandleGitData.convertArrowPath (l.Substring(3))
                        let s1 = HandleGitData.convertToStatus l.[0]
                        let s2 = HandleGitData.convertToStatus l.[1]
                        Option.Some { 
                            Local = { Status = s1; Path = p1}; 
                            Server = { Status = s2; Path = p2}
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
                if (System.String.IsNullOrEmpty(l)) then Option.None
                else Option.Some l
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
                    if (System.String.IsNullOrEmpty(l)) then Option.None
                    else
                        Option.Some {
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
                    if l = null || l.Length = 0 
                    then Option.None
                    else
                        let items = 
                            l.Split([|' '; '\t'|])
                                |> Seq.map (fun e -> e.Trim())
                                |> Seq.filter (fun e -> not (System.String.IsNullOrWhiteSpace e))
                                |> Seq.toArray
                        Option.Some {
                            Name = items.[0];
                            Url = items.[1];
                            Type = 
                                if items.[2].Contains("fetch") 
                                then Fetch
                                else Push
                        }
                )
        }
    static member private RunProgressGitCommand(gitProc:GitProcess, onProcessChange) = 
        async {
            let progressRegex = 
                new System.Text.RegularExpressions.Regex (
                    @"([0-9]+)%", 
                    System.Text.RegularExpressions.RegexOptions.Compiled);
            return!
                gitProc.RunWithErrorOutputAsync((fun o -> Option.None), fun e ->
                    if not (System.String.IsNullOrEmpty(e)) then 
                        let matching = progressRegex.Match(e)
                        if (matching.Success) then
                            let progress = System.Double.Parse(matching.Groups.[1].Value) / 100.0
                            let compressingPart = 0.25;
                            onProcessChange
                                (progress * 
                                    (if (e.StartsWith("Compressing")) then
                                        compressingPart
                                     else
                                        1.0 - compressingPart))
                    Option.None
                )
        }
    static member RunGitFetchAsync(location, url, branch, onProcessChange) = 
        async {
            use gitProc = new GitProcess(location, GitArguments.Fetch(url, branch))
            do! GitProcess.RunProgressGitCommand(gitProc, onProcessChange) |> Async.Ignore
        }

    static member RunGitRebaseAsync(location, rebaseType) = 
        async {
            use gitProc = new GitProcess(location, GitArguments.Rebase(rebaseType))
            do! gitProc.RunAsync(id)
        }

    static member RunGitAddAsync(location, addOption, files) = 
        async {
            use gitProc = new GitProcess(location, GitArguments.Add(addOption, files))
            do! gitProc.RunAsync(id)
        }

    static member RunGitCommitAsync(location, message) = 
        async {
            use gitProc = new GitProcess(location, GitArguments.Commit(message))
            do! gitProc.RunAsync(id)
        }

    static member RunGitPushAsync(location, url, branch, onProcessChange) = 
        async {
            use gitProc = new GitProcess(location, GitArguments.Push(url, branch))
            do! GitProcess.RunProgressGitCommand(gitProc, onProcessChange) |> Async.Ignore
        }
    static member RunGitCheckoutAsync(location, checkoutType) = 
        async {
            use gitProc = new GitProcess(location, GitArguments.Checkout(checkoutType))
            do! gitProc.RunAsync(id)
        }
