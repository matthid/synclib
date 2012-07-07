// ----------------------------------------------------------------------------
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.
// ----------------------------------------------------------------------------
module SvnTesting

open System

open Yaaf.SyncLib
open Yaaf.SyncLib.Svn

// Creating a backendmanager (required once per backend)
let backendManager = new SvnBackendManager() :> IBackendManager
   
let createManager = BackendTester.createManager backendManager