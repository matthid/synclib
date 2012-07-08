// ----------------------------------------------------------------------------
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.
// ----------------------------------------------------------------------------
namespace Yaaf.SyncLib

open System.Threading.Tasks

/// A simple info object to contain all the settings for a specific backend. Extensible over the IDictionary
type ManagedFolderInfo(name:string, path:string, remote:string, announcementUrl:string, dict:System.Collections.Generic.IDictionary<string,string>) = 
    let dictCopy = new System.Collections.Generic.Dictionary<_,_>(dict) :> System.Collections.Generic.IDictionary<_,_>
    /// The name or key shown in the gui for the folder, has to be unique (possibly used by the backend)
    member x.Name = name
    /// The full path of the folder (used by the backend)
    member x.FullPath = path 
    /// The remote string for the folder (used by the backend)
    member x.Remote = remote
    /// A serverurl which can be listened to for changes (can be used by the backend)
    member x.AnnouncementUrl = announcementUrl
    /// Additional settings to be extensible (other backends could have other configurations requirements)
    member x.Additional = dictCopy

/// The Type of Conflict that occured while syncing
type SyncConflict = 
    | MergeConflict of string
    | Unknown of string

/// The state of the syncing process
type SyncState = 
    /// The folder is idle and waiting for changes
    | Idle     = 0
    /// The folder is processing a syncup
    | SyncUp   = 1
    /// The folder is processing a syncdown
    | SyncDown = 2
    /// The server for the folder is offline
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
  
/// A simple type to create the Mangers (this can be dynamically loaded in the future for example)
type IBackendManager = 
    /// Inits the 
    abstract member CreateFolderManager : ManagedFolderInfo -> IManagedFolder

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

    /// The implementation to add events or start/stop the service
    member x.Implementation with get() = implementation
    /// The current state of the implementation
    member x.State with get() = state
    /// The current progress of the current operation (0 when no operation is running)
    member x.Progress 
        with get() = 
            if (state = SyncState.SyncDown || state = SyncState.SyncUp) 
            then progress
            else 0.0

