namespace SyncLib

open System.Threading.Tasks

type ManagedFolderInfo(name:string, path:string, remote:string, backend:string, announcementUrl:string, dict:System.Collections.Generic.IDictionary<string,string>) = 
    member x.Name = name
    member x.FullPath = path 
    member x.Remote = path
    member x.Backend = backend
    member x.AnnouncementUrl = announcementUrl
    member x.Additional 
        with get(key:string) = 
            dict.[key]

type SyncConflict = 
    | Unknown of string

type SyncState = 
    | Idle     = 0
    | SyncUp   = 1
    | SyncDown = 2
    | Offline  = 3

/// Represents a managed folder (will be kept in sync automatically)
type IManagedFolder = 
    /// Starts the sync service
    abstract member StartService : unit -> unit

    /// Stops the sync service for this folder
    abstract member StopService : unit -> unit

    /// Indicates the start of a sync
    [<CLIEvent>]
    abstract member SyncStateChanged : IEvent<SyncState>
        
    /// Indicates a sync error
    [<CLIEvent>]
    abstract member SyncError : IEvent<System.Exception>
    
    /// Indicates a conflict while syncing (will be resolved automatically, just notify)
    [<CLIEvent>]
    abstract member SyncConflict : IEvent<SyncConflict>
       
    /// Indicates a progress - change in the sync process
    [<CLIEvent>]
    abstract member ProgressChanged : IEvent<double>
  
type IBackendManager = 
    /// Inits the 
    abstract member createFolderManager : ManagedFolderInfo -> IManagedFolder

/// A simple wrapper around the interface which keeps track over the state
type ManagedFolderWrapper(impl:IManagedFolder) = 
    let implementation = impl
    let mutable state = SyncState.Idle
    let mutable progress = 0.0
    do 
        impl.SyncStateChanged
            |> Event.add 
                (fun newstate -> 
                    progress <- 0.0
                    state <- newstate)

        impl.ProgressChanged
            |> Event.add (fun newProgress -> progress <- newProgress)

    member x.Implementation with get() = implementation
    member x.State with get() = state
    member x.Progress 
        with get() = 
            if (state = SyncState.SyncDown || state = SyncState.SyncUp) 
            then progress
            else 0.0

