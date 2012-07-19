// ----------------------------------------------------------------------------
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.
// ----------------------------------------------------------------------------
namespace Yaaf.SyncLib.Svn


open Yaaf.SyncLib
open Yaaf.SyncLib.Svn

type SvnBackendManager() = 
     
    let possibleSvnPaths = 
        ["/usr/bin/svn"
         "/usr/local/bin/svn"
         "/opt/local/bin/svn"
         "/usr/local/git/bin/svn"
         "C:\\Program Files\\TortoiseSVN\\bin\\svn.exe" ]
    let locatePath paths def= 
        (match 
            paths
                |> List.tryFind (fun path -> System.IO.File.Exists(path)) with
        | Option.Some(foundPath) -> foundPath
        | Option.None -> def)
        
    let svnPath = lazy locatePath possibleSvnPaths "svn"

    member x.CreateFolderManager(folder:ManagedFolderInfo) = 
        let newDict = 
            folder.Additional
                |> Map.tryAdd "svnpath" svnPath

        if not <| System.IO.Directory.Exists(folder.FullPath) then
            System.IO.Directory.CreateDirectory(folder.FullPath) |> ignore

        new SvnRepositoryFolder({ folder with Additional=newDict }) :> ISyncFolderFolder
    
    interface IBackendManager with
        member x.CreateFolderManager(folder) = x.CreateFolderManager(folder)