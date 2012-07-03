namespace SyncLib.Git


open SyncLib
open SyncLib.Git

type GitBackendManager() = 
    member x.CreateFolderManager(folder) = 
        new GitRepositoryFolder(folder) :> IManagedFolder
    
    interface IBackendManager with
        member x.CreateFolderManager(folder) = x.CreateFolderManager(folder)

