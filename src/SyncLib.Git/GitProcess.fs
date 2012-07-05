// Weitere Informationen zu F# unter "http://fsharp.net".

namespace SyncLib.Git

open System.Diagnostics
open System.IO
open SyncLib
open SyncLib.Helpers
open SyncLib.Helpers.AsyncTrace

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

module GitProcess = 
    let createGitProc (loc:string) (args:GitArguments) = 
        new ToolProcess(HandleGitArguments.locateGit(), loc, HandleGitArguments.toCommandLine(args))

    let runGitProgressCommand(gitProc:ToolProcess, onProcessChange) = 
        asyncTrace() {
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
    let RunGitStatusAsync(location) = 
        asyncTrace() {
            use gitProc = createGitProc location Status
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

    let RunGitStatus(location) = 
        RunGitStatusAsync(location)
            |> convertToAsync
            |> Async.RunSynchronously

    let RunGitLsRemoteAsync(location, uri, branch) = 
        asyncTrace() {
            use gitProc = createGitProc location (Ls_remote(uri, branch))
            let! output = gitProc.RunWithOutputAsync(fun l -> 
                if (System.String.IsNullOrEmpty(l)) then Option.None
                else Option.Some l
                )

            return output.[0].Substring(0,40)
        }

    let RunGitLsRemote(location, uri, branch) = 
        RunGitLsRemoteAsync(location, uri, branch)
            |> convertToAsync
            |> Async.RunSynchronously
      

    let RunGitInitAsync(location) = 
        asyncTrace() {
            use gitProc = createGitProc location Init
            do! gitProc.RunAsync()
            return ()
        }

    let RunGitBranchAsync(location, types) = 
        asyncTrace() {
            use gitProc = createGitProc location (Branch(types))
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

    let RunGitRemoteAsync(location, types) = 
        asyncTrace() {
            use gitProc = createGitProc location (Remote(types))
            
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
    
    let RunGitFetchAsync(location, url, branch, onProcessChange) = 
        asyncTrace() {
            use gitProc = createGitProc location (GitArguments.Fetch(url, branch))
            do! runGitProgressCommand(gitProc, onProcessChange) |> AsyncTrace.Ignore
        }

    let RunGitRebaseAsync(location, rebaseType) = 
        asyncTrace() {
            use gitProc = createGitProc location (GitArguments.Rebase(rebaseType))
            do! gitProc.RunAsync()
        }

    let RunGitAddAsync(location, addOption, files) = 
        asyncTrace() {
            use gitProc = createGitProc location (GitArguments.Add(addOption, files))
            do! gitProc.RunAsync()
        }

    let RunGitCommitAsync(location, message) = 
        asyncTrace() {
            use gitProc = createGitProc location (GitArguments.Commit(message))
            do! gitProc.RunAsync()
        }

    let RunGitPushAsync(location, url, branch, onProcessChange) = 
        asyncTrace() {
            use gitProc = createGitProc location (GitArguments.Push(url, branch))
            do! runGitProgressCommand(gitProc, onProcessChange) |> AsyncTrace.Ignore
        }
    let RunGitCheckoutAsync(location, checkoutType) = 
        asyncTrace() {
            use gitProc = createGitProc location (GitArguments.Checkout(checkoutType))
            do! gitProc.RunAsync()
        }
