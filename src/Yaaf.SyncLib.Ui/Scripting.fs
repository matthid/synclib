// ----------------------------------------------------------------------------
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.
// ----------------------------------------------------------------------------
namespace Yaaf.SyncLib.Ui

open Yaaf.SyncLib
open Yaaf.SyncLib.Ui
open Yaaf.SyncLib.Git
open Yaaf.SyncLib.Svn
open Yaaf.SyncLib.Ui.PosixHelper


open System

open Gtk
open System.IO
open Mono.Unix

type BackendType = 
    | Git
    | Svn

module Scripting = 
    
    let gitBackendManager = new GitBackendManager() :> IBackendManager
    let svnBackendManager = new SvnBackendManager() :> IBackendManager

    let CustomManager (backendManager:IBackendManager) info = 
        info, backendManager.CreateFolderManager info

    let BackendInfo name folder server announceUrl additionalInfo = 
        new ManagedFolderInfo (
            name, folder, server, announceUrl, additionalInfo)

    let Manager backendType name folder server = 
        let backend = 
            match backendType with
            | Git -> gitBackendManager
            | Svn -> svnBackendManager
        let info =
            BackendInfo name folder server "" (new System.Collections.Generic.Dictionary<_,_>())
        CustomManager 
            backend
            info

    let RunGui (managers:(ManagedFolderInfo * IManagedFolder) list)  =
        Application.Init()
        catalogInit "Yaaf.SyncLib.Ui" "./lang"
        let icon = StatusIcon.NewFromStock(Stock.Info)
        icon.Tooltip <- CString "Update Notification Icon"
        //icon.set_FromFile(

        let menu = new Menu() 
        let quitItem = new ImageMenuItem(CString "Quit")

        quitItem.Image <- new Image(Stock.Quit, IconSize.Menu)
        
        icon.PopupMenu 
            |> Event.add (fun args -> 
                    menu.Popup(null, null, new MenuPositionFunc(fun menu x y push_in -> StatusIcon.PositionMenu(menu, ref x, ref y, ref push_in, icon.Handle)), 0u, Global.CurrentEventTime);
                    GtkUtils.bringToForeground()
                )

        quitItem.Activated
            |> Event.add (fun args -> 
                    Application.Quit()    
                )
        
        for info, manager in managers do
            
            let managerItem = new ImageMenuItem(CString info.Name)
            
            managerItem.Image <- new Image(Stock.Directory, IconSize.Menu)
            managerItem.Activated
                |> Event.add (fun args ->
                        System.Diagnostics.Process.Start(info.FullPath) |> ignore)
            manager.SyncError
                |> Event.add (fun error ->
                        use md = new MessageDialog (null, DialogFlags.Modal, MessageType.Info, ButtonsType.Ok, sprintf "Error: %s" (error.ToString()))
                        md.Run () |> ignore
                        md.Destroy())
            
            menu.Add(managerItem)
            manager.StartService()
        
        menu.Add(quitItem)
        menu.ShowAll() 
        Application.Run()

        for info, manager in managers do manager.StopService()
