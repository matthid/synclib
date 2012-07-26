// ----------------------------------------------------------------------------
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.
// ----------------------------------------------------------------------------
namespace Yaaf.SyncLib.Git

open System.Diagnostics
open System.IO
open Yaaf.SyncLib
open Yaaf.AsyncTrace

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
type SubmoduleType = 
    | Add of string * string
    | Status 
    | Init of string list
    | Update of string list
    | Sync of string list

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
    | Submodule of SubmoduleType

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
                | GitAddType.Update -> "-u"
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
        | Submodule (subType) ->
            sprintf "submodule %s"
                (match subType with
                | SubmoduleType.Add(url, path) -> sprintf "add \"%s\" %s" url path
                | SubmoduleType.Init(files) -> sprintf "init --%s" (files|>parseFiles)
                | SubmoduleType.Status -> sprintf "status --recursive"
                | SubmoduleType.Sync(files) -> sprintf "sync --%s" (files|>parseFiles)
                | SubmoduleType.Update(files) -> sprintf "update --%s" (files|>parseFiles))

type GitStatusType = 
    | Added    = 0
    | Modified = 1
    | Deleted  = 2
    | Renamed  = 3
    | Copied   = 4
    | Updated  = 5
    | None     = 6
    | Unknown  = 7
/// The status given by git submodule status
type GitSubmoduleStatusType = 
    /// ( ) All normal 
    | None     = 0
    /// (-) Not initialized
    | NotInitialized = 1   
    /// (+) Submodule changed  
    | Changed = 2   
    /// (U) Submodule merge conflict
    | MergeConflict = 3

       
type GitFileStatus = {
    Status : GitStatusType;
    Path : string }
type GitStatus =   { 
        Local : GitFileStatus;
        Server : GitFileStatus;
    }
type GitSubmoduleStatus = {
        Status : GitSubmoduleStatusType
        Sha1:string
        Branch :string
        Path :string 
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
    /// Resolves an ansi string given by git and outputs the utf8 string
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
    /// Resolves the given path to UTF8 if required
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
    let convertSubmoduleStatus c = 
        match c with
        | ' ' -> GitSubmoduleStatusType.None
        | '-' -> GitSubmoduleStatusType.NotInitialized
        | '+' -> GitSubmoduleStatusType.Changed
        | 'U' -> GitSubmoduleStatusType.MergeConflict
        | _ -> failwith "unknown Submodule Status code"
    let convertArrowPath (p:string) = 
        let i = p.IndexOf(" -> ")
        if (i <> -1) then
            convertToResovedPath (p.Substring (0, i)),
            convertToResovedPath (p.Substring (i + 4))
        else
            let cp = convertToResovedPath p
            cp, cp

    let parseStatusLine (l:string) = 
        let p1, p2 = convertArrowPath (l.Substring(3))
        let s1 = convertToStatus l.[0]
        let s2 = convertToStatus l.[1]
        { 
            Local = { Status = s1; Path = p1}; 
            Server = { Status = s2; Path = p2}
        }
    let parseSubmoduleStatusLine (l:string) = 
        let splits = l.Substring(1).Split([|' '|])
        if (splits.Length < 2) then failwith "invalid submodule status line"
        let last = splits.[splits.Length - 1]
        // BUG: not 100% but pretty close
        // The path could be something line "test/blub (branch)" this would be 
        // recognised as "test/blub" with branch "branch"
        // On windows we could check if there is a "/" in the last string,
        // On linux not even this would work.
        let containsBranch = splits.Length > 2 && last.StartsWith("(") && last.EndsWith(")")
        {
            Status = convertSubmoduleStatus l.[0]
            Sha1 = splits.[0]
            Branch = 
                if containsBranch then
                    splits.[splits.Length - 1]
                else ""
            Path = System.String.Join(" ", splits |> Seq.skip 1 |> Seq.take (splits.Length - (if containsBranch then 2 else 1)))
        }



/// Permission of the given file was denied (can be "unknown filename") 
exception GitPermissionDenied of string
/// Push was rejected
exception GitRejected
/// We are stuck on rebase (git tells us to run --abort, --continue or --skip)
exception GitStuckRebase
/// We have no master Branch (can happen when we have no initial commit)
exception GitNoMasterBranch
/// We are not in a git repository
exception GitNoRepository
/// Unstaged changes
exception GitUnstagedChanges
/// Git merge conflict
exception GitMergeConflict
/// When git got an invalid working-dir
exception GitInvalidWorkingDir

/// Access to git commands
module GitProcess = 
    let tracer = new TraceSource("Yaaf.SyncLib.Git.GitProcess")
    let handleGitErrorLine l = 
        match l with
        // unable to unlink old 'Microsoft Word Document (neu).docx' (Permission denied)
        | StartsWith "error: unable to unlink old '" rest when l.Contains("Permission denied") ->
            let endFileName = rest.IndexOf("'")
            let fileName =
                if (endFileName <> -1) then rest.Substring(0, endFileName)
                else "unknown filename"
            raise (GitPermissionDenied(fileName))
        | Contains "Permission denied" -> raise (GitPermissionDenied("unknown filename"))
        | Contains "! [rejected]" -> raise GitRejected
        | Contains "git rebase (--continue | --abort | --skip)" -> raise GitStuckRebase
        | Contains "fatal: no such branch: master" 
        | Contains "error: src refspec master does not match any"-> raise GitNoMasterBranch
        | Contains "fatal: Not a git repository (or any of the parent directories): .git"-> raise GitNoRepository
        | Contains "Cannot rebase: You have unstaged changes."-> raise GitUnstagedChanges
        | Contains "Cannot merge"-> raise GitMergeConflict
        | Contains "ssh: connect to host localdevserver port 22: Bad file number"-> raise OfflineException
        | Contains "fatal: The remote end hung up unexpectedly"-> raise (ConnectionException l)
        | _ -> ()
    let gitErrorFun e = 
        handleGitErrorLine e
        None

    let runAdvanced runFun args git wDir  = asyncTrace() {
        use gitProc = new ToolProcess(git, wDir, HandleGitArguments.toCommandLine(args))
        // NOTE: it can happen that the git process gets stuck!
        // This usually indicates that we got a "The remote end hang up unexpectedly"
        // A possible fix would be to add a timeout option to the ToolProcess and 
        // handle it on the git side with killing all child processes of git
        // (or even better the whole process tree from the bottom up
        // Also note that you have to do this also on the other Run methods 
        // (which are currently not factored out into functions).
        
        try
            return! runFun gitProc 
        with ToolProcessFailed(255, failedCmd, output, error) ->
            let wDirInvalid = GitInvalidWorkingDir
            wDirInvalid.Data.["Output"] <- output
            wDirInvalid.Data.["Error"] <- error
            wDirInvalid.Data.["ExitCode"] <- 255
            wDirInvalid.Data.["Cmd"] <- failedCmd
            return (raise wDirInvalid)
        } 
    let run = 
        runAdvanced 
            (fun p -> 
                p.RunWithErrorOutputAsync(
                    (fun _ -> None), 
                    gitErrorFun) 
                |> AsyncTrace.Ignore)

    /// Runs a git progress command (fetch or push)
    let runGitProgressCommand onProcessChange = 
            let progressRegex = 
                new System.Text.RegularExpressions.Regex (
                    @"([0-9]+)%", 
                    System.Text.RegularExpressions.RegexOptions.Compiled)
            runAdvanced(
                (fun (gitProc:ToolProcess) -> 
                    gitProc.RunWithErrorOutputAsync(
                        (fun o -> Option.None), 
                        fun e ->
                            handleGitErrorLine e
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
                            Option.None) |> AsyncTrace.Ignore))
    let onlyOutput t = asyncTrace() {
            let! fst, snd = t
            return fst
        }
    /// Runs git status
    let status = 
        runAdvanced 
            (fun gitProc -> 
                gitProc.RunWithErrorOutputAsync((HandleGitData.parseStatusLine >> Some), gitErrorFun)
                |> onlyOutput) 
            Status
    /// Runs git status and waits for it to finish
    let statusSync git wDir = 
        status git wDir
            |> AsyncTrace.SetTracer (Logging.DefaultTracer tracer "Running Git Status")
            |> Async.RunSynchronously

    /// runs git ls-remote
    let lsRemote uri branch = 
        runAdvanced 
            (fun gitProc -> asyncTrace() {
                let! output = 
                    gitProc.RunWithErrorOutputAsync(
                        (fun l -> 
                            if (System.String.IsNullOrEmpty(l)) then Option.None
                            else Option.Some l),
                        gitErrorFun) |> onlyOutput

                return output.[0].Substring(0,40) }) 
            (Ls_remote(uri,branch))
        
    /// Runs git --ls-remote synchronous
    let lsRemoteSync uri branch git wDir  = 
        lsRemote uri branch git wDir
            |> AsyncTrace.SetTracer (Logging.DefaultTracer tracer "Running Git Ls Remote")
            |> Async.RunSynchronously
      
    /// Runs git init
    let init = run Init
    /// Runs git branch
    let branch types = 
        runAdvanced 
            (fun gitProc ->
                gitProc.RunWithErrorOutputAsync(
                    (fun l ->
                        if (System.String.IsNullOrEmpty(l)) then Option.None
                        else
                            Option.Some {
                                IsSelected = (l.[0] = '*');
                                Name = l.Substring(2) 
                            }),
                    gitErrorFun) |> onlyOutput)
            (Branch(types))
        
    /// Runs git remote
    let remote types = 
        runAdvanced
            (fun gitProc ->
                gitProc.RunWithErrorOutputAsync((fun l ->
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
                        }),
                    gitErrorFun) |> onlyOutput)
                (Remote(types))
    /// Runs git fetch
    let fetch url branch onProcessChange = 
        runGitProgressCommand onProcessChange (GitArguments.Fetch(url, branch))
    /// Runs git rebase
    let rebase rebaseType = 
        run (GitArguments.Rebase(rebaseType))
    /// Runs git add
    let add addOption files = 
        run (GitArguments.Add(addOption, files))
    /// Runs git commit
    let commit message = 
        run (GitArguments.Commit(message))
    /// Runs git push
    let push url branch onProcessChange = 
        runGitProgressCommand onProcessChange (GitArguments.Push(url, branch))
    /// Runs git checkout
    let checkout checkoutType = 
        run (GitArguments.Checkout(checkoutType))
    /// Runs a git submodule command
    let submodule submoduleType = 
        run (GitArguments.Submodule(submoduleType))
    /// Runs the git submodule status command
    let submoduleStatus () = 
        runAdvanced 
            (fun gitProc -> 
                gitProc.RunWithErrorOutputAsync((HandleGitData.parseSubmoduleStatusLine >> Some), gitErrorFun)
                |> onlyOutput) 
            (GitArguments.Submodule SubmoduleType.Status)