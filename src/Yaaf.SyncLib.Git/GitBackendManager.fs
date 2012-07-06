// ----------------------------------------------------------------------------
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.
// ----------------------------------------------------------------------------
namespace Yaaf.SyncLib.Git


open Yaaf.SyncLib
open Yaaf.SyncLib.Git

type GitBackendManager() = 
    member x.CreateFolderManager(folder) = 
        new GitRepositoryFolder(folder) :> IManagedFolder
    
    interface IBackendManager with
        member x.CreateFolderManager(folder) = x.CreateFolderManager(folder)

