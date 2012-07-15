// ----------------------------------------------------------------------------
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.
// ----------------------------------------------------------------------------
#I @"..\Yaaf.SyncLib\bin\Debug"
#I @"bin\Debug"
#r "Yaaf.AsyncTrace.dll"
#r "Yaaf.SyncLib.dll"
#r "Yaaf.SyncLib.Svn.dll"

open Yaaf.AsyncTrace

open Yaaf.SyncLib
open Yaaf.SyncLib.Svn

let tracer = new System.Diagnostics.TraceSource("Yaaf.SyncLib.Svn.Script")

let data = 
    SvnProcess.status @"C:\Program Files\TortoiseSVN\bin\svn.exe" @"D:\Test"  
    |> Logging.SetDefaultTracer tracer "svn status"
    |> Async.RunSynchronously