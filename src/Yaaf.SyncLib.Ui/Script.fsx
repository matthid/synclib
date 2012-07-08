// Diese Datei ist ein Skript, das mit F# interaktiv ausgeführt werden kann.  
// Es kann zur Erkundung und zum Testen des Bibliotheksprojekts verwendet werden.
// Skriptdateien gehören nicht zum Projektbuild.
// ----------------------------------------------------------------------------
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.
// ----------------------------------------------------------------------------
// The next Lines are always required
#I @"libs"
#r "Yaaf.SyncLib.Ui.dll"
open Yaaf.SyncLib
open Scripting

// Your startup logic / your folders
let myManagers = [
    Manager Git "MyName" "D:\\Documents" "git@localdevserver:mydata.git" ]

// Starting Tray Icon
RunGui myManagers
    
