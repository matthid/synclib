namespace SyncLib

type IChangeWatcher = 
    [<CLIEvent>]
    abstract member Changed : IEvent<unit>

/// Opt in Api to watch local changes
type LocalChangeWatcher(folder : ManagedFolderInfo)  = 
    let changedEvent = new Event<unit>()
    let timer = new System.Threading.Timer(
                    (fun t -> changedEvent.Trigger(())), null, System.TimeSpan.FromMinutes(5.0), System.TimeSpan.FromMinutes(5.0))
    interface IChangeWatcher with
        [<CLIEvent>]
        member x.Changed = changedEvent.Publish

   
/// Opt in Api to watch remote changes
type RemoteChangeWatcher(folder : ManagedFolderInfo) = 
    let changedEvent = new Event<unit>()
    let timer = new System.Threading.Timer(
                    (fun t -> changedEvent.Trigger(())), null, System.TimeSpan.FromMinutes(5.0), System.TimeSpan.FromMinutes(5.0))
    interface IChangeWatcher with
        [<CLIEvent>]
        member x.Changed = changedEvent.Publish


