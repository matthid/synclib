// ----------------------------------------------------------------------------
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.
// ----------------------------------------------------------------------------
namespace Yaaf.SyncLib.Svn


open Yaaf.SyncLib
open Yaaf.AsyncTrace


open System.Diagnostics
open System.IO
///The first column indicates that an item was added, deleted, or otherwise changed:
type SvnStatusLineChangeType =
    /// ( ) No modifications.
    | None
    /// (A) Item is scheduled for addition.
    | Added
    /// (D) Item is scheduled for deletion.
    | Deleted
    /// (M) Item has been modified.
    | Modified
    /// (R) Item has been replaced in your working copy. This means the file was scheduled for deletion, and then a new file with the same name was scheduled for addition in its place.
    | Replaced
    /// (C) The contents (as opposed to the properties) of the item conflict with updates received from the repository.
    | ContentConflict
    /// (X) Item is present because of an externals definition.
    | ExternalDefinition
    /// (I) Item is being ignored (e.g., with the svn:ignore property).
    | Ignored
    /// (?) Item is not under version control.
    | NotInSourceControl
    /// (!) Item is missing (e.g., you moved or deleted it without using svn). This also indicates that a directory is incomplete (a checkout or update was interrupted).
    | ItemMissing
    /// (~) Item is versioned as one kind of object (file, directory, link), but has been replaced by a different kind of object.
    | MissVersioned

///The second column tells the status of a file's or directory's properties:
type SvnStatusProperties = 
    /// ( ) No modifications.
    | None
    /// (M) Properties for this item have been modified.
    | PropModified
    /// (C) Properties for this item are in conflict with property updates received from the repository.
    | PropConflict

///The sixth column is populated with lock information:
type SvnStatusLockInformation =     
    /// ( ) When --show-updates (-u) is used, the file is not locked. If --show-updates (-u) is not used, this merely means that the file is not locked in this working copy.
    | None
    /// (K) File is locked in this working copy.
    | FileLocked
    /// (O) File is locked either by another user or in another working copy. This appears only when --show-updates (-u) is used.
    | LockedByOtherUser
    /// (T) File was locked in this working copy, but the lock has been “stolen” and is invalid. The file is currently locked in the repository. This appears only when --show-updates (-u) is used.
    | LockStolen
    /// (B) File was locked in this working copy, but the lock has been “broken” and is invalid. The file is no longer locked. This appears only when --show-updates (-u) is used.
    | LockInvalid

/// status of working copy files and directories.
type SvnStatusLine = {
    /// The first column indicates that an item was added, deleted, or otherwise changed:
    ChangeType : SvnStatusLineChangeType
    /// The second column tells the status of a file's or directory's properties:
    Properties : SvnStatusProperties
    /// (L) The third column is populated only if the working copy directory is locked (see the section called “Sometimes You Just Need to Clean Up”)
    IsLocked : bool
    /// (+) The fourth column is populated only if the item is scheduled for addition-with-history:
    ScheduledForAddingWithHistory : bool
    /// (S) The fifth column is populated only if the item is switched relative to its parent (see the section called “Traversing Branches”):
    SwitchedRelative : bool
    /// The sixth column is populated with lock information:
    LockInformation : SvnStatusLockInformation
    /// (C) The seventh column is populated only if the item is the victim of a tree conflict:
    IsTreeConflict : bool
    /// The eighth column is always blank.
    /// (*) The out-of-date information appears in the ninth column (only if you pass the --show-updates (-u) option):
    IsOutOfDate : bool
    /// The remaining fields are variable width and delimited by spaces. The working revision is the next field if the --show-updates (-u) or --verbose (-v) option is passed.
    Revision : int option
    /// If the --verbose (-v) option is passed, the last committed revision and last committed author are displayed next.
    /// The working copy path is always the final field, so it can include spaces.
    FilePath : string
    }

/// Data returned by the "svn info" command
type SvnInfo = {
    /// .
    Path : string
    /// /home/me/folder
    WorkingDirRoot : string
    /// https://server.com/svn/root/trunk/CurrentFolder
    Url : string
    /// https://server.com/svn/root
    RepositoryRoot : string
    /// 454b96c5-00da-4618-90ec-f94890f0cf31
    RepositoryUUID : string
    /// 123
    Revision : int
    /// directory
    Kind : string
    /// normal
    Schedule : string
    /// empty
    Depth : string
    /// blub@gmail.com
    LastAuthor : string
    /// 26
    LastRevision : int
    /// 2012-05-06 17:02:39 +0200 (So, 06 Mai 2012)
    LastChanged : System.DateTime
    }

type SvnUpdateType =
    /// ( ) None (if only Properties are updated)
    | None
    /// (A) Added 
    | Added
    /// (D) Deleted
    | Deleted
    /// (U) Updated
    | Updated
    /// (C) Conflict
    | Conflicting
    /// (G) Merged
    | Merged
    
type SvnUpdateInfo = 
    /// Updated to revision 32.
    | FinishedRevision of int
    /// A  newdir/launch.c (UpdateType, PropertyUpdateType, Filename
    | FinishedFile of SvnUpdateType * SvnUpdateType * string
module HandleSvnData = 
    /// this will will parse a line given by "svn status --show-updates"
    let parseStatusLine (l:string) = 
        if (l.[7] <> ' ') then failwith "The eighth column should be blank"
        let restParams = l.Substring(9).Split([|' '|])
        {
        ChangeType =
            match l.[0] with
            | ' ' -> SvnStatusLineChangeType.None
            | 'A' -> SvnStatusLineChangeType.Added
            | 'D' -> SvnStatusLineChangeType.Deleted
            | 'M' -> Modified
            | 'R' -> Replaced
            | 'C' -> ContentConflict
            | 'X' -> ExternalDefinition
            | 'I' -> Ignored
            | '?' -> NotInSourceControl
            | '!' -> ItemMissing
            | '~' -> MissVersioned
            | _ -> failwith (sprintf "Unknown svn changetype %c (first status column), line: %s" l.[0] l)
        Properties = 
            match l.[1] with
            | ' ' -> SvnStatusProperties.None
            | 'M' -> SvnStatusProperties.PropModified
            | 'C' -> SvnStatusProperties.PropConflict
            | _ -> failwith (sprintf "Unknown svn property %c (secound status column), line: %s" l.[1] l)

        IsLocked = 
            match l.[2] with
            | ' ' -> false
            | 'L' -> true
            | _ -> failwith (sprintf "Unknown svn property %c (third status column), line: %s" l.[2] l)

        ScheduledForAddingWithHistory =
            match l.[3] with
            | ' ' -> false
            | '+' -> true
            | _ -> failwith (sprintf "Unknown svn property %c (fourth status column), line: %s" l.[3] l)
            
        SwitchedRelative =
            match l.[4] with
            | ' ' -> false
            | 'S' -> true
            | _ -> failwith (sprintf "Unknown svn property %c (fifth status column), line: %s" l.[4] l)

        LockInformation = 
            match l.[5] with
            | ' ' -> SvnStatusLockInformation.None
            | 'K' -> SvnStatusLockInformation.FileLocked
            | 'O' -> SvnStatusLockInformation.LockedByOtherUser
            | 'T' -> SvnStatusLockInformation.LockStolen
            | 'B' -> SvnStatusLockInformation.LockInvalid
            | _ -> failwith (sprintf "Unknown svn property %c (sixth status column), line: %s" l.[5] l)
           
        IsTreeConflict = 
            match l.[6] with
            | ' ' -> false
            | 'C' -> true
            | _ -> failwith (sprintf "Unknown svn property %c (seventh status column), line: %s" l.[6] l)
        IsOutOfDate = 
            match l.[8] with
            | ' ' -> false
            | '*' -> true
            | _ -> failwith (sprintf "Unknown svn property %c (ninth status column), line: %s" l.[8] l)
        Revision = 
            match System.Int32.TryParse(restParams.[0]) with
            | true, value -> Some value
            | _ -> Option.None
        FilePath = System.String.Join(" ", restParams |> Seq.skip 1).Trim() 
        }
    let parseUpdateLine line = 
        match line with
            | StartsWith "Updated to revision " rest -> //Rest="234."
                FinishedRevision(System.Int32.Parse(rest.Substring(0, rest.Length-1)))
            | _ ->
                let filePath = line.Substring(2).Trim()
                let svnUpdateType = 
                    match line.[0] with
                    | ' ' -> SvnUpdateType.None
                    | 'A' -> SvnUpdateType.Added
                    | 'C' -> SvnUpdateType.Conflicting
                    | 'D' -> SvnUpdateType.Deleted
                    | 'G' -> SvnUpdateType.Merged
                    | 'U' -> SvnUpdateType.Updated
                    | _ -> failwith (sprintf "Unknown SVN Update type: \"%c\", line: %s" line.[0] line)
                let svnUpdatePropertyType = 
                    match line.[1] with
                    | ' ' -> SvnUpdateType.None
                    | 'A' -> SvnUpdateType.Added
                    | 'C' -> SvnUpdateType.Conflicting
                    | 'D' -> SvnUpdateType.Deleted
                    | 'G' -> SvnUpdateType.Merged
                    | 'U' -> SvnUpdateType.Updated
                    | _ -> failwith (sprintf "Unknown SVN Update type: \"%c\", line: %s" line.[1] line)
                FinishedFile (svnUpdateType, svnUpdatePropertyType, filePath)

    let parseInfoDataLine (defaults:System.Collections.Generic.IDictionary<_,_>) state line =
        let rec handleLineRec (i,list) =
            let currentLine = 
                match i with
                | 1 -> "Path: " // .
                | 2 -> "Name: " //readme.doc
                | 3 -> "Working Copy Root Path: " // /home/me/folder
                | 4 -> "URL: " // https://server.com/svn/root/trunk/Blatt0
                | 5 -> "Repository Root: " // https://server.com/svn/root
                | 6 -> "Repository UUID: " // 454b96c5-00da-4618-90ec-f94890f0cf31
                | 7 -> "Revision: " // 123
                | 8 -> "Node Kind: " // directory
                | 9 -> "Schedule: " // normal
                | 10 -> "Depth: " // normal
                | 11 -> "Last Changed Author: " // blub@gmail.com
                | 12 -> "Last Changed Rev: " // 26
                | 13 -> "Last Changed Date: " // 2012-05-06 17:02:39 +0200 (So, 06 Mai 2012)
                | _ -> failwith (sprintf "SVN: too much info lines \"%s\"" line)
            match line with
            | StartsWith currentLine rest -> 
                (i+1, rest :: list)
            | _ ->
                match i with
                | 2 ->
                    // Get the name from the Path.
                    match list with
                    | h :: t -> handleLineRec (i+1, Path.GetDirectoryName h :: list)
                    | _ -> failwith (sprintf "expected that we did parse the path (Line \"%s\")" line)
                | _ ->
                    if not (defaults.ContainsKey(i)) then 
                        failwith (sprintf "Unknown SVN Line %s, expected %s" line currentLine)
                    handleLineRec (i+1, defaults.[i] :: list)
        handleLineRec state

    let parseInfoData infoData = 
        let defaults =
            new System.Collections.Generic.Dictionary<_,_>()
        defaults.[10] <- "infinite" // default for depth
        defaults.[11] <- "None" // Last changed author
        let count, output =
            infoData 
                |> Seq.fold 
                    (parseInfoDataLine defaults)
                    (1, [])

        if count <> 14 then failwith (sprintf "Invalid number of info lines!")
                
        match output |> List.rev with
        | [path; name; workingDirRoot; url; repRoot; repUUID; rev; kind; schedule; depth; lastAuthor; lastRev; lastChanged ] -> 
            {
                Path = path
                WorkingDirRoot = workingDirRoot
                Url = url
                RepositoryRoot = repRoot
                RepositoryUUID = repUUID
                Revision = System.Int32.Parse rev
                Kind = kind
                Schedule = schedule
                Depth = depth
                LastAuthor = lastAuthor
                LastRevision = System.Int32.Parse lastRev
                LastChanged = System.DateTime.Parse(lastChanged.Substring(0,19))
            }
        | _ -> failwith "invalid svn info data received!"

exception SvnNotWorkingDir
module SvnProcess = 
    let svnErrorFun error = 
        match error with
        | ContainsAll ["warning: W155007: ";" is not a working copy"] -> raise SvnNotWorkingDir
        | ContainsAll ["svn: E155007: "; " is not a working copy"] -> raise SvnNotWorkingDir
        //| ContainsAll ["svn: E155007: "; " is not a working copy"] -> raise SvnNotWorkingDir
        | _ -> Option.None

    let runSvn rfun param svn local   = asyncTrace() {
        let svnProc = new ToolProcess(svn, local, param)
        return! rfun(svnProc) }

    let runSvnSimple = runSvn (fun proc -> proc.RunAsync())

    let checkout remote = runSvnSimple  (sprintf "checkout -r 0 \"%s\" ." remote)
        
    let resolved file = runSvnSimple (sprintf "resolved \"%s\"" file)

    let add file = runSvnSimple (sprintf "add \"%s\"" file)
        
    let delete file = runSvnSimple (sprintf "remove \"%s\"" file)

    let commit message = runSvnSimple (sprintf "commit -m \"%s\"" message)
        
    let status = 
        runSvn 
            (fun svnProc -> asyncTrace() {
                let! o, e =
                    svnProc.RunWithErrorOutputAsync(
                        (fun line -> 
                            match line with
                            /// Ignore last Line
                            | Contains "Status against revision:" -> Option.None
                            /// Parse Line
                            | _ -> Some <| HandleSvnData.parseStatusLine line), 
                        svnErrorFun)
                return o })
            (sprintf "status --show-updates")

    let info svn local = 
        runSvn 
            (fun svnProc -> asyncTrace() {
                let! o, e =
                    svnProc.RunWithErrorOutputAsync(
                        (fun line ->
                            match line with
                            | Equals "" -> Option.None
                            | _ -> Some <| line),
                        svnErrorFun)
                        
                let outputData = 
                    HandleSvnData.parseInfoData o

                return outputData })
            (sprintf "info")

    let update receivedLine = 
        runSvn 
            (fun svnProc -> 
                    svnProc.RunWithErrorOutputAsync(
                        (fun line ->
                            if (line <> "") then  
                                match line with 
                                | StartsWith "Updating " rest -> () // "'.':"    
                                | StartsWith "At revision " rest -> () // "133."    
                                | _ -> line |> HandleSvnData.parseUpdateLine |> receivedLine
                            Option.None),
                        svnErrorFun) |> AsyncTrace.Ignore)
            (sprintf "update")