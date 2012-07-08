// ----------------------------------------------------------------------------
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.
// ----------------------------------------------------------------------------
namespace Yaaf.SyncLib.Ui

open Yaaf.SyncLib
open Yaaf.SyncLib.Ui
open Yaaf.SyncLib.Git
//open Yaaf.SyncLib.Svn


open System

open Gtk
open System.IO
type BackendType = 
    | Git
    //| Svn

module Scripting = 
    
    let gitBackendManager = new GitBackendManager() :> IBackendManager
    //let svnBackendManager = new SvnBackendManager() :> IBackendManager
    let Manager backendType name folder server = 
        let backend,typeName = 
            match backendType with
            | Git -> gitBackendManager, "git"
            //| Svn -> svnBackendManager, "svn"

        backend.CreateFolderManager 
            (new ManagedFolderInfo(
                name, 
                folder,
                server,
                "",
                new System.Collections.Generic.Dictionary<_,_>()))

    let CustomManager (backendManager:IBackendManager) info = backendManager.CreateFolderManager info
    let BackendInfo name folder server announceUrl additionalInfo = 
        new ManagedFolderInfo (
            name, folder, server, announceUrl, additionalInfo)

    let RunGui (managers:IManagedFolder list) =
        Application.Init()
        let icon = StatusIcon.NewFromStock(Stock.Info)
        icon.Tooltip <- "Update Notification Icon"

        let menu = new Menu() 
        let quitItem = new ImageMenuItem("Quit")

        quitItem.Image <- new Image(Stock.Quit, IconSize.Menu)
        menu.Add(quitItem)
        menu.ShowAll() 
            
        icon.PopupMenu 
            |> Event.add (fun args -> 
                    menu.Popup(null, null, new MenuPositionFunc(fun menu x y push_in -> StatusIcon.PositionMenu(menu, ref x, ref y, ref push_in, icon.Handle)), 0u, Global.CurrentEventTime);
                    GtkUtils.bringToForeground()
                )

        quitItem.Activated
            |> Event.add (fun args -> 
                    Application.Quit()    
                )

        Application.Run();
