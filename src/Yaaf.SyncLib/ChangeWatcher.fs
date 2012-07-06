// ----------------------------------------------------------------------------
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.
// ----------------------------------------------------------------------------
namespace Yaaf.SyncLib

open Yaaf.SyncLib.Helpers

/// A interface for notifications on changes.
type IChangeWatcher = 
    /// The Changed event will be triggered when a change occured.
    [<CLIEvent>]
    abstract member Changed : IEvent<unit>

/// Opt in Api to watch local changes, will trigger on any folder changes
type SimpleLocalChangeWatcher(folder : string, onError)  = 
    let changedEvent = new Event<System.IO.WatcherChangeTypes * string * string>()
    let watcher = new System.IO.FileSystemWatcher(folder, "*")
    do
        watcher.IncludeSubdirectories <- true
        watcher.EnableRaisingEvents <- true
        watcher.NotifyFilter <-
            System.IO.NotifyFilters.Attributes ||| System.IO.NotifyFilters.CreationTime ||| System.IO.NotifyFilters.DirectoryName
            ||| System.IO.NotifyFilters.FileName ||| System.IO.NotifyFilters.LastAccess ||| System.IO.NotifyFilters.LastWrite
            ||| System.IO.NotifyFilters.Security ||| System.IO.NotifyFilters.Size

        // Watch folder
        watcher.Changed
            |> Event.map (fun args -> args.ChangeType, args.FullPath, args.FullPath) 
            |> Event.merge (watcher.Created |> Event.map (fun args -> args.ChangeType, "", args.FullPath))
            |> Event.merge (watcher.Deleted |> Event.map (fun args -> args.ChangeType, args.FullPath, ""))
            |> Event.merge (watcher.Renamed |> Event.map (fun args -> args.ChangeType, args.OldFullPath, args.FullPath))
            |> Event.add (fun args -> changedEvent.Trigger args)
      
        watcher.Error |> Event.add (fun er -> onError(er.GetException()))

    member x.Changed = changedEvent.Publish
    interface IChangeWatcher with
        [<CLIEvent>]
        member x.Changed = changedEvent.Publish |> Event.map (fun args -> ()) 

type IntelligentLocalWatcher(folder:string, onError) = 
    let changedEvent = new Event<System.IO.WatcherChangeTypes * string * string>()
    let simpleWatcher = new SimpleLocalChangeWatcher(folder, onError)
    do
        simpleWatcher.Changed
            // This esures that every 2 minutes there is only 1 Event at maximum
            // (lots of events will be reduced to 1 ... and this 1 will be fired when there was no event for 1 min)
            |> Event.reduceTime (System.TimeSpan.FromMinutes(1.0))
            |> Event.add (fun args -> changedEvent.Trigger args) 
 
    member x.Changed = changedEvent.Publish
    interface IChangeWatcher with
        [<CLIEvent>]
        member x.Changed = changedEvent.Publish |> Event.map (fun args -> ()) 
   
/// Opt in Api to watch remote changes
type RemoteChangeWatcher(folder : ManagedFolderInfo) = 
    let changedEvent = new Event<unit>()
    let timer = new System.Threading.Timer(
                    (fun t -> changedEvent.Trigger(())), null, System.TimeSpan.FromMinutes(5.0), System.TimeSpan.FromMinutes(5.0))
    interface IChangeWatcher with
        [<CLIEvent>]
        member x.Changed = changedEvent.Publish


