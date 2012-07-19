// ----------------------------------------------------------------------------
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.
// ----------------------------------------------------------------------------
// The next Lines are always required
#I @"lib"
#I @"bin\Debug\lib"
#r "Yaaf.SyncLib.dll"
#r "Yaaf.SyncLib.Ui.dll"
open Yaaf.SyncLib
open Yaaf.SyncLib.Ui
open Yaaf.SyncLib.Ui.Scripting

//HideFsi()

// Your startup logic / your folders
let myManagers = [
        // Edit the following lines to represent your repositories (NOTE: whitespace is important in F#)
        CustomManager 
            Git 
            (BackendInfo 
                "GitReproName" 
                "C:\\users\\me\\documents\\mygitrepro" 
                "git@mygitserver2:repro.git"
                (Map.ofList [("PubsubUrl",     "tcp://notifications.sparkleshare.org:80");
                             ("PubsubChannel", "akhgfjkasbhdfasdf" )]))
                
        CustomManager 
            Git 
            {
                Name = "GitReproName" 
                FullPath = "C:\\users\\me\\documents\\mygitrepro" 
                Remote = "git@mygitserver2:repro.git"
                Additional = Map.ofList [("PubsubUrl",     "tcp://notifications.sparkleshare.org:80");
                                         ("PubsubChannel", "akhgfjkasbhdfasdf" )]
            }

        // Add a git repository note the "GitRepro/Test" is the name and no folder
        Manager Git "GitRepro/Test" "C:\\users\\me\\documents\\mygitrepro" "git@mygitserver:repro.git"

        // Add a svn repository
        Manager Svn "SvnRepro" "/home/me/svnrepr1" "https://server1.com/svn/root/subfolder"

        // Add a svn repository but expain it
        Manager 
            Svn // the type of the manager (currently Svn or Git)
            "SvnRepro2"  // The name of the repro (this will be shown in the menu)
            "/home/me/svnrepro2" // Path to my folder path (where i want my repository to sit)
            "https://server2.com/svn/root" // the url of the server

        // Stop editing here if you don't know what you are doing
    ]

// Starting Tray Icon
RunGui myManagers
    
