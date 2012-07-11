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


module Scripting = 
    
    module private InterOp =
        [<System.Runtime.InteropServices.DllImport("user32.dll")>]
        extern bool ShowWindow(nativeint hWnd, int flags)

        let HideProcWindow(proc:System.Diagnostics.Process) = 
            ShowWindow(proc.MainWindowHandle, 0) |> ignore

    let Git = new GitBackendManager() :> IBackendManager
    let Svn = new SvnBackendManager() :> IBackendManager

    let HideFsi () = 
        InterOp.HideProcWindow (System.Diagnostics.Process.GetCurrentProcess())

    let CustomManager (backendManager:IBackendManager) info = 
        info, backendManager.CreateFolderManager info

    let BackendInfo name folder server additionalInfo = 
        new ManagedFolderInfo (
            name, folder, server, additionalInfo)

    let Manager backend name folder server = 
        let info =
            BackendInfo name folder server (new System.Collections.Generic.Dictionary<_,_>())
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

    // This is maybe a solution for future version to make logging possible via script file
    // For now just copy the fsi.exe and create a fsi.exe.config
//    [<AbstractClass>]
//    type AppConfig() =
//        static member Change( path) =
//            new ChangeAppConfig(path)
//        abstract member Dispose : unit -> unit
//        interface IDisposable with
//            member x.Dispose() = x.Dispose()
//
//    and ChangeAppConfig(path:string) =   
//        inherit AppConfig()
//
//        let mutable disposedValue = false
//
//        
//        let ResetConfigMechanism() =
//            let managerType = typedefof<System.Configuration.ConfigurationManager>
//            let bindingFlags = System.Reflection.BindingFlags.NonPublic |||
//                               System.Reflection.BindingFlags.Static
//            managerType
//                .GetField("s_initState",bindingFlags)
//                .SetValue(null, 0);
//
//            managerType
//                .GetField("s_configSystem", bindingFlags)
//                .SetValue(null, null);
//
//            (managerType
//                .Assembly.GetTypes()
//                |> Seq.filter 
//                    (fun x -> 
//                        x.FullName = "System.Configuration.ClientConfigPaths")
//                |> Seq.head)
//                .GetField("s_current", bindingFlags)
//                .SetValue(null, null);
//
//        do 
//            AppDomain.CurrentDomain.SetData("APP_CONFIG_FILE", path)
//            ResetConfigMechanism()
//
//
//        
//        override x.Dispose() =
//            if not (disposedValue) then
//                AppDomain.CurrentDomain.SetData("APP_CONFIG_FILE", ChangeAppConfig.oldConfig)
//                ResetConfigMechanism()
//                disposedValue <- true
//            GC.SuppressFinalize(x)
//
//        static member private oldConfig =
//            AppDomain.CurrentDomain.GetData("APP_CONFIG_FILE").ToString()
    


