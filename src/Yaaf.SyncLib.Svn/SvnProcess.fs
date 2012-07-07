// ----------------------------------------------------------------------------
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.
// ----------------------------------------------------------------------------
namespace Yaaf.SyncLib.Svn



open System.Diagnostics
open System.IO
open Yaaf.SyncLib
open Yaaf.SyncLib.Helpers
open Yaaf.SyncLib.Helpers.AsyncTrace

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
    /// File is locked either by another user or in another working copy. This appears only when --show-updates (-u) is used.
    | LockedByOtherUser
    /// File was locked in this working copy, but the lock has been “stolen” and is invalid. The file is currently locked in the repository. This appears only when --show-updates (-u) is used.
    | LockStolen
    ///File was locked in this working copy, but the lock has been “broken” and is invalid. The file is no longer locked. This appears only when --show-updates (-u) is used.
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
    Revision : int
    /// If the --verbose (-v) option is passed, the last committed revision and last committed author are displayed next.
    /// The working copy path is always the final field, so it can include spaces.
    FilePath : string
    }

module HandleSvnData = 
    let parseStatusLine l = 
        l

module SvnProcess = 
    
    let checkout svn local remote = asyncTrace() {
        let svnProc = new ToolProcess(svn, local, sprintf "checkout \"%s\" ." remote)
        do! svnProc.RunAsync() }

    let resolved svn local file = asyncTrace() {
        let svnProc = new ToolProcess(svn, local, sprintf "resolved \"%s\"" file)
        do! svnProc.RunAsync() }

    let status svn local = asyncTrace() {
        let svnProc = new ToolProcess(svn, local, sprintf "status --show-updates")
        return! svnProc.RunWithOutputAsync (HandleSvnData.parseStatusLine >> Some)
    }