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
open Yaaf.AsyncTrace

module Scripting = 
    let scriptTrace = Logging.DefaultTracer (Logging.Source "Yaaf.SyncLib.Ui.Scripting") "ScriptingRun"
    module private InterOp =
        [<System.Runtime.InteropServices.DllImport("user32.dll")>]
        extern bool ShowWindow(nativeint hWnd, int flags)

        let HideProcWindow(proc:System.Diagnostics.Process) = 
            ShowWindow(proc.MainWindowHandle, 0) |> ignore

    let Git = new GitBackendManager() :> IBackendManager
    let Svn = new SvnBackendManager() :> IBackendManager
        
    /// given an Data dictionary we will return all available remote data server
    let extractRemoteConnectionData (dataDict:System.Collections.Generic.IDictionary<string,string>) =
        seq {
            match dataDict |> Dict.tryGetValue "PubsubUrl", 
                  dataDict |> Dict.tryGetValue "PubsubChannel" with
            | Some (pubsubUri), Some(pubsubChannel) -> 
                yield Pubsub(new System.Uri(pubsubUri),pubsubChannel)
            | _ -> ()
        }

    type BackendInfo = ManagedFolderInfo

    let HideFsi () = 
        InterOp.HideProcWindow (System.Diagnostics.Process.GetCurrentProcess())
    let EmptyManager  (backendManager:IBackendManager) (info:ManagedFolderInfo) = 
        let syncFolder = backendManager.CreateFolderManager info
        let manager =
            syncFolder
                |> Sync.toManaged
        info, manager
    let AddLocalWatcher time ignoreFolders (info,folder) = 
        let localWatcher = 
            Notify.localWatcher info.FullPath
            |> Event.filter 
                (fun (changeType, oldPath, newPath) -> 
                    not (ignoreFolders
                        |> Seq.exists
                            (fun folder ->
                                let fullPath = Path.Combine(info.FullPath, folder)
                                oldPath.StartsWith fullPath || newPath.StartsWith fullPath)))
            // Reduce event
            |> Event.reduceTime time
            |> Notify.fromEvent
        info,
        folder
            |> Sync.addLocalNotifier localWatcher

    let AddRemotesFromInfo (info, folder) = 
        let remoteProvider =
            info.Additional
            |> extractRemoteConnectionData 
            |> doPipe (fun s -> if s |> Seq.length = 0 then scriptTrace.logWarn "No remote data for %s!" info.Name)
            |> RemoteConnectionManager.calculateMergedEvent
            |> Notify.fromRemoteEvents (Guid.NewGuid().ToString())
        info,
        folder 
            |> Sync.addRemoteNotifier remoteProvider
    let AddRemote provider (info, folder) = 
        info,
        folder
            |> Sync.addRemoteNotifier provider

    let AddRemoteFromData data info = 
        let provider =
            RemoteConnectionManager.remoteDataToEvent data
                    |> Notify.fromRemoteEvents (Guid.NewGuid().ToString())    
        info
            |> AddRemote provider

    let CustomManager (backendManager:IBackendManager) (info:ManagedFolderInfo) = 
        let info, manager = EmptyManager backendManager info
        let ignoreFolders = 
            match backendManager with
            | Equals Git -> [".git"]
            | Equals Svn -> [".svn"]
            | _ -> []
            
        (info, manager )
            |> AddRemotesFromInfo
            |> AddLocalWatcher (System.TimeSpan.FromMinutes(1.0)) ignoreFolders

    let BackendInfo name folder server additionalInfo = {
        Name = name
        FullPath = folder
        Remote = server
        Additional = additionalInfo
    }

    let Manager backend name folder server = 
        let info = BackendInfo name folder server Map.empty
        CustomManager backend info

    let doOnGdk f = 
        Gtk.Application.Invoke(fun sender args -> f())

    let RunGui (managers:(ManagedFolderInfo * IManagedFolder) list)  =
        try
            scriptTrace.logVerb "Starting UI"
            Gtk.Application.Init ();
            catalogInit "Yaaf.SyncLib.Ui" "./lang"
            let icon = StatusIcon.NewFromStock(Stock.Info)
            icon.Tooltip <- CString "Update Notification Icon"
            //icon.set_FromFile(

            let menu = new Menu() 
            let quitItem = new ImageMenuItem(CString "Quit")

            quitItem.Image <- new Image(Stock.Quit, IconSize.Menu)
        
            icon.PopupMenu 
                |> Event.add (fun args -> 
                        menu.Popup()
                        GtkUtils.bringToForeground()
                    )

            quitItem.Activated
                |> Event.add (fun args -> 
                        icon.Visible <- false
                        Application.Quit()    
                    )
        
            for info, manager in managers do
            
                let managerItem = new ImageMenuItem(CString info.Name)
                let image = new Image(Stock.Directory, IconSize.Menu)
                managerItem.Image <- image
                managerItem.Activated
                    |> Event.add (fun args ->
                            System.Diagnostics.Process.Start(info.FullPath) |> ignore)
                manager.Folder.SyncStateChanged
                    |> Event.add (fun state ->
                        doOnGdk (fun _ ->
                            match state with
                            | SyncState.SyncError(t, error) ->
                                try
                                doOnGdk (fun _ ->
                                    printfn "Trying to display: %A" error
                                    icon.Stock <- Stock.DialogError
                                    icon.Blinking <- true
                                    let md = new MessageDialog (null, DialogFlags.Modal, MessageType.Info, ButtonsType.Ok, false, "{0}", [| ((sprintf "Error: %A" error):>obj) |])
                                    md.UseMarkup <- false
                                    md.Run() |> ignore
                                    let myLog m = 
                                        printfn "%s" m; scriptTrace.logInfo "%s" m
                                    md.ButtonPressEvent    
                                        |> Event.add (fun e -> myLog "pressed")
                                    md.ButtonReleaseEvent  
                                        |> Event.add (fun e -> myLog "released")
                                    
                                    md.Close
                                        |> Event.add (fun t -> md.Destroy()))
                                    // md.Run () |> ignore
                                    //md.Destroy())
                                with exn -> scriptTrace.logErr "Error in MessageDialog %O" exn
                            | SyncState.Idle -> 
                                icon.Blinking <- false
                                icon.Stock <- Stock.Info
                                image.Stock <- Stock.Directory
                            | SyncState.Offline ->
                                icon.Blinking <- false
                                icon.Stock <- Stock.DialogWarning
                                image.Stock <- Stock.DialogWarning
                            | SyncState.SyncStart(s) ->
                                let iconImage =
                                    match s with
                                    | SyncUp -> Stock.GoUp
                                    | SyncDown -> Stock.GoDown
                                icon.Blinking <- true
                                icon.Stock <-iconImage
                                image.Stock <- iconImage
                            | _ as syncState-> 
                                scriptTrace.logErr "Unknown syncstate %O" syncState
                                icon.Stock <- Stock.DialogError
                                image.Stock <- Stock.DialogError
                                icon.Blinking <- true
                            )
                        )
            
                menu.Add(managerItem)
                manager.Init()
                manager.StartService()
        
            menu.Add(quitItem)
            menu.ShowAll() 
            Application.Run()

            for info, manager in managers do manager.StopService()
        with exn -> scriptTrace.logCrit "Error in RunGui %O" exn

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
    


