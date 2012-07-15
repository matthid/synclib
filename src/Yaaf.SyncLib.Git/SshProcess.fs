// ----------------------------------------------------------------------------
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.
// ----------------------------------------------------------------------------
namespace Yaaf.SyncLib.Git

open System.Diagnostics
open System.IO
open Yaaf.SyncLib
open Yaaf.AsyncTrace

exception SshAuthException of string
exception SshConnectionException of string * string
module SshProcess =
    let handleErrorLine remote s = 
        match s with
        | Contains "debug2: no key of type 2 for host" -> 
            raise(SshAuthException(sprintf "The authenticity of host %s can't be established." remote))
        | Contains "ssh: connect to host localdevserver port 22: Bad file number" ->
            raise (SshConnectionException(
                    sprintf "cannot connect to remote server %s" remote, 
                    "ssh: connect to host localdevserver port 22: Bad file number"))
        | _ -> ()
    let ensureConnection remote ssh wDir = asyncTrace() {        
        let sshProc = new ToolProcess(ssh, wDir, sprintf "-t -t -v -v %s" remote)
        
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
                (fun errorLine -> handleErrorLine remote errorLine; None)) 
            |> AsyncTrace.Ignore }

    