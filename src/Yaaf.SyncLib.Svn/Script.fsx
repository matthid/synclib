// ----------------------------------------------------------------------------
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.
// ----------------------------------------------------------------------------
#I @"..\Yaaf.SyncLib\bin\Debug"
#I @"bin\Debug"
#r "Yaaf.SyncLib.dll"
#r "Yaaf.SyncLib.Svn.dll"

open Yaaf.SyncLib
open Yaaf.SyncLib.Svn
open Yaaf.SyncLib.Helpers
open Yaaf.SyncLib.Helpers.AsyncTrace

let data = 
    SvnProcess.status @"C:\Program Files\TortoiseSVN\bin\svn.exe" @"D:\Test"  
    |> AsyncTrace.SetDefaultTracer "Debug" 
    |> Async.RunSynchronously