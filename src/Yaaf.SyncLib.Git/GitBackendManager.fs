// ----------------------------------------------------------------------------
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.
// ----------------------------------------------------------------------------
namespace Yaaf.SyncLib.Git


open Yaaf.SyncLib
open Yaaf.SyncLib.Git

type GitBackendManager() = 
     
    let possibleGitPaths = 
        ["/usr/bin/git";
         "/usr/local/bin/git"; 
         "/opt/local/bin/git"; 
         "/usr/local/git/bin/git";
         "C:\\Program Files (x86)\\Git\\bin\\git.exe";
         "msysgit\\bin\\git.exe"; ]
    let possibleSshPaths = 
        ["/usr/bin/ssh";
         "/usr/local/bin/ssh"; 
         "/opt/local/bin/ssh"; 
         "/usr/local/git/bin/ssh";
         "C:\\Program Files (x86)\\Git\\bin\\ssh.exe";
         "msysgit\\bin\\ssh.exe"; ]

    let locatePath paths def= 
        (match 
            paths
                |> List.tryFind (fun path -> System.IO.File.Exists(path)) with
        | Option.Some(foundPath) -> foundPath
        | Option.None -> def)
        
    let locateGit() = locatePath possibleGitPaths "git"
    let locateSsh() = locatePath possibleSshPaths "ssh"
    let checkKey (dict:System.Collections.Generic.IDictionary<_,_>) key f = 
        if not (dict.ContainsKey(key)) then
            dict.[key] <- f()

    member x.CreateFolderManager(folder:ManagedFolderInfo) = 
        checkKey folder.Additional "gitpath" locateGit
        checkKey folder.Additional "sshpath" locateSsh

        new GitRepositoryFolder(folder) :> IManagedFolder
    
    interface IBackendManager with
        member x.CreateFolderManager(folder) = x.CreateFolderManager(folder)

