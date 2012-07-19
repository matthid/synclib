// ----------------------------------------------------------------------------
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.
// ----------------------------------------------------------------------------
namespace Yaaf.SyncLib.Git


open Yaaf.SyncLib
open Yaaf.SyncLib.Git
open Yaaf.SyncLib.Helpers.Map

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
        
    let gitPath = lazy locatePath possibleGitPaths "git"
    let sshPath = lazy locatePath possibleSshPaths "ssh"

    member x.CreateFolderManager(folder:ManagedFolderInfo) = 
        let newDict =
            folder.Additional 
                |> Map.tryAdd "gitPath" gitPath
                |> Map.tryAdd "sshpath" sshPath

        if not <| System.IO.Directory.Exists(folder.FullPath) then
            System.IO.Directory.CreateDirectory(folder.FullPath) |> ignore

        new GitRepositoryFolder({ folder with Additional = newDict }) :> IManagedFolder
    
    interface IBackendManager with
        member x.CreateFolderManager(folder) = x.CreateFolderManager(folder)

