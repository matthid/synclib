// ----------------------------------------------------------------------------
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.
// ----------------------------------------------------------------------------
namespace Yaaf.SyncLib.Svn

open Yaaf.SyncLib
open Yaaf.SyncLib.Helpers
open Yaaf.SyncLib.Helpers.AsyncTrace
open Yaaf.SyncLib.Svn

open System.IO

/// Syncronises a git folder
type SvnRepositoryFolder(folder:ManagedFolderInfo) as x =  
    inherit RepositoryFolder(folder)

    let localWatcher = new SimpleLocalChangeWatcher(folder.FullPath, (fun err -> x.ReportError err))
    let remoteWatcher = new RemoteChangeWatcher(folder)

    let progressChanged = new Event<double>()
    let syncConflict = new Event<SyncConflict>()

    /// Indicates wheter this svn rep is initialized (ie requires special checks at startup)
    let mutable isInit = false
    let svnPath = folder.Additional.["svnpath"]

    let init() = asyncTrace() {


        return ()
    }

    let syncDown() = asyncTrace() {
        let! (t:ITracer) = AsyncTrace.traceInfo()
        progressChanged.Trigger 0.0
        try
            if not isInit then do! init()

            t.logInfo "Starting Syncdown of %s" folder.Name

//            // Fetch changes
//            do! GitProcess.RunGitFetchAsync 
//                    git 
//                    folder.FullPath 
//                    remoteName
//                    "master"
//                    (fun newProgress -> progressChanged.Trigger (newProgress * 0.95))
//                |> AsyncTrace.Ignore
//
//            // Merge changes into local directory via "git rebase FETCH_HEAD"
//            do! commitAllChanges()
//            try
//                t.logInfo "Starting SyncDown-Merging of %s" folder.Name
//                do! GitProcess.RunGitRebaseAsync git folder.FullPath (Start("FETCH_HEAD", "master"))
//            with
//                | ToolProcessFailed(exitCode, cmd, o, e) ->
//                    match e with
//                    | Contains "fatal: no such branch: master" ->
//                        repairMasterBranch()
//                        x.RequestSyncDown()
//                    | _ ->
//                        let errorMsg = (sprintf "Cmd: %s, Code: %d, Output: %s, Error %s" cmd exitCode o e)
//                        t.logWarn "Conflict while Down-Merging of %s: %s" folder.Name errorMsg
//                        // Conflict
//                        syncConflict.Trigger (SyncConflict.Unknown errorMsg)
//                    
//                        // Resolve conflict
//                        do! resoveConflicts()
        finally 
            progressChanged.Trigger 1.0
    }

    let syncUp() = asyncTrace() {
        return ()
    }    
    
    override x.StartSyncDown () = 
        syncDown()

    override x.StartSyncUp() = 
        syncUp()
        
    [<CLIEvent>]
    override x.ProgressChanged = progressChanged.Publish

    [<CLIEvent>]
    override x.SyncConflict = syncConflict.Publish