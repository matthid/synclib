// ----------------------------------------------------------------------------
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.
// ----------------------------------------------------------------------------
namespace Yaaf.SyncLib.Git

open System.Diagnostics
open System.IO
open Yaaf.SyncLib
open Yaaf.SyncLib.Helpers
open Yaaf.SyncLib.Helpers.AsyncTrace

exception SshException of string * string
module SshProcess =
    let ensureConnection ssh wDir remote automaticAccept = asyncTrace() {        
        let sshProc = new ToolProcess(ssh, wDir, sprintf "-t -t -v -v %s" remote)
        try
            do! sshProc.RunWithErrorOutputAsync(
                    (fun outputLine ->
                         // NOTE: To catch this kind of output you would actually have to write a "virtual terminal device"
                         // If we would do this there we could capture the output of git itself.
                         // http://www.unix.com/shell-programming-scripting/152781-bash-capturing-anything-showed-screen.html
                         // http://stackoverflow.com/questions/307006/catching-a-direct-redirect-to-dev-tty
                            
    //                    if (outputLine.Contains("Are you sure you want to continue connecting (yes/no)?")) then
    //                        sshProc.StandardInput.WriteLine(
    //                            if (automaticAccept) then "yes" else "no")
                        None),
                    (fun errorLine -> 
                        if (errorLine <> null && errorLine.Contains("debug2: no key of type 2 for host")) then
                            sshProc.Kill()
                        None)) 
                |> AsyncTrace.Ignore
        with
            | ToolProcessFailed(exitcode, cmd, output, error) -> 
                raise(SshException(sprintf "The authenticity of host %s can't be established." remote, error)) }

    