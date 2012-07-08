// ----------------------------------------------------------------------------
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.
// ----------------------------------------------------------------------------
namespace Yaaf.SyncLib.Svn


open Yaaf.SyncLib
open Yaaf.SyncLib.Helpers
open Yaaf.SyncLib.Helpers.AsyncTrace
open Yaaf.SyncLib.Helpers.MatchHelper


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
    /// blub@gmail.com
    LastAuthor : string
    /// 26
    LastRevision : int
    /// 2012-05-06 17:02:39 +0200 (So, 06 Mai 2012)
    LastChanged : System.DateTime
    }

type SvnUpdateType =
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
    /// A  newdir/launch.c
    | FinishedFile of SvnUpdateType * string
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
            | _ -> failwith (sprintf "Unknown svn changetype %c (first status column)" l.[0])
        Properties = 
            match l.[1] with
            | ' ' -> SvnStatusProperties.None
            | 'M' -> SvnStatusProperties.PropModified
            | 'C' -> SvnStatusProperties.PropConflict
            | _ -> failwith (sprintf "Unknown svn property %c (secound status column)" l.[1])

        IsLocked = 
            match l.[2] with
            | ' ' -> false
            | 'L' -> true
            | _ -> failwith (sprintf "Unknown svn property %c (third status column)" l.[2])

        ScheduledForAddingWithHistory =
            match l.[3] with
            | ' ' -> false
            | '+' -> true
            | _ -> failwith (sprintf "Unknown svn property %c (fourth status column)" l.[3])
            
        SwitchedRelative =
            match l.[4] with
            | ' ' -> false
            | 'S' -> true
            | _ -> failwith (sprintf "Unknown svn property %c (fifth status column)" l.[4])

        LockInformation = 
            match l.[5] with
            | ' ' -> SvnStatusLockInformation.None
            | 'K' -> SvnStatusLockInformation.FileLocked
            | 'O' -> SvnStatusLockInformation.LockedByOtherUser
            | 'T' -> SvnStatusLockInformation.LockStolen
            | 'B' -> SvnStatusLockInformation.LockInvalid
            | _ -> failwith (sprintf "Unknown svn property %c (sixth status column)" l.[5])
           
        IsTreeConflict = 
            match l.[6] with
            | ' ' -> false
            | 'C' -> true
            | _ -> failwith (sprintf "Unknown svn property %c (seventh status column)" l.[6])
        IsOutOfDate = 
            match l.[8] with
            | ' ' -> false
            | '*' -> true
            | _ -> failwith (sprintf "Unknown svn property %c (ninth status column)" l.[8])
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
                let svnType = 
                    match line.[0] with
                    | 'A' -> SvnUpdateType.Added
                    | 'C' -> SvnUpdateType.Conflicting
                    | 'D' -> SvnUpdateType.Deleted
                    | 'G' -> SvnUpdateType.Merged
                    | 'U' -> SvnUpdateType.Updated
                    | _ -> failwith (sprintf "Unknown SVN Update type: \"%c\"" line.[0])
                FinishedFile (svnType, filePath)

exception SvnNotWorkingDir
module SvnProcess = 
    let svnErrorFun error = 
        match error with
        | ContainsAll ["warning: W155007: ";" is not a working copy"] -> raise SvnNotWorkingDir
        | ContainsAll ["svn: E155007: "; " is not a working copy"] -> raise SvnNotWorkingDir
        | _ -> Option.None
    let checkout svn local remote = asyncTrace() {
        let svnProc = new ToolProcess(svn, local, sprintf "checkout \"%s\" ." remote)
        do! svnProc.RunAsync() }

    let resolved svn local file = asyncTrace() {
        let svnProc = new ToolProcess(svn, local, sprintf "resolved \"%s\"" file)
        do! svnProc.RunAsync() }

    let status svn local = asyncTrace() {
        let svnProc = new ToolProcess(svn, local, sprintf "status --show-updates")
        let! output, error = 
            svnProc.RunWithErrorOutputAsync(
                (fun line -> 
                    match line with
                    /// Ignore last Line
                    | Contains "Status against revision:" -> Option.None
                    /// Parse Line
                    | _ -> Some <| HandleSvnData.parseStatusLine line), 
                svnErrorFun)
        return output
    }

    let info svn local = asyncTrace() {
        let svnProc = new ToolProcess(svn, local, sprintf "info")
        let! output, error = 
            svnProc.RunWithErrorOutputAsync(
                (fun line ->
                    match line with
                    | Equals "" -> Option.None
                    | _ -> 
                        Some <|
                            match line with
                            | StartsWith "Path: " rest -> rest // .
                            | StartsWith "Working Copy Root Path: " rest -> rest // /home/me/folder
                            | StartsWith "URL: " rest -> rest // https://server.com/svn/root/trunk/Blatt0
                            | StartsWith "Repository Root: " rest -> rest // https://server.com/svn/root
                            | StartsWith "Repository UUID: " rest -> rest // 454b96c5-00da-4618-90ec-f94890f0cf31
                            | StartsWith "Revision: " rest -> rest // 123
                            | StartsWith "Node Kind: " rest -> rest // directory
                            | StartsWith "Schedule: " rest -> rest // normal
                            | StartsWith "Last Changed Author: " rest -> rest // blub@gmail.com
                            | StartsWith "Last Changed Rev: " rest -> rest // 26
                            | StartsWith "Last Changed Date: " rest -> rest // 2012-05-06 17:02:39 +0200 (So, 06 Mai 2012)
                            | _ -> failwith (sprintf "SVN: unknown info line \"%s\"" line)),
                svnErrorFun)
        
        let outputData = 
            match output |> Seq.toList with
            | [path; workingDirRoot; url; repRoot; repUUID; rev; kind; schedule; lastAuthor; lastRev; lastChanged ] -> 
                {
                    Path = path
                    WorkingDirRoot = workingDirRoot
                    Url = url
                    RepositoryRoot = repRoot
                    RepositoryUUID = repUUID
                    Revision = System.Int32.Parse rev
                    Kind = kind
                    Schedule = schedule
                    LastAuthor = lastAuthor
                    LastRevision = System.Int32.Parse lastRev
                    LastChanged = System.DateTime.Parse(lastChanged.Substring(0,19))
                }
            | _ -> failwith "invalid svn info data received!"

        return outputData
    }

    let update svn local receivedLine = asyncTrace() {
        let svnProc = new ToolProcess(svn, local, sprintf "update")
        do! svnProc.RunWithErrorOutputAsync(
                (fun line ->
                    if (line <> "") then                           
                        line |> HandleSvnData.parseUpdateLine |> receivedLine
                    Option.None),
                svnErrorFun)
            |> AsyncTrace.Ignore
    }