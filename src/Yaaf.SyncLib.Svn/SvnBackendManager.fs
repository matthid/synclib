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
        
    let locateSvn() = locatePath possibleSvnPaths "svn"
    let checkKey (dict:System.Collections.Generic.IDictionary<_,_>) key f = 
        if not (dict.ContainsKey(key)) then
            dict.[key] <- f()

    member x.CreateFolderManager(folder:ManagedFolderInfo) = 
        checkKey folder.Additional "svnpath" locateSvn

        if not <| System.IO.Directory.Exists(folder.FullPath) then
            System.IO.Directory.CreateDirectory(folder.FullPath) |> ignore

        new SvnRepositoryFolder(folder) :> IManagedFolder
    
    interface IBackendManager with
        member x.CreateFolderManager(folder) = x.CreateFolderManager(folder)