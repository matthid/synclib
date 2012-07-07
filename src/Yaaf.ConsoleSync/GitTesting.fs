// ----------------------------------------------------------------------------
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.
// ----------------------------------------------------------------------------

module GitTesting

open Yaaf.SyncLib
open Yaaf.SyncLib.Git

// Creating a backendmanager (required once per backend)
let backendManager = new GitBackendManager() :> IBackendManager
   
let createManager = BackendTester.createManager backendManager