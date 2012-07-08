# SyncLib 

First of all please make sure if you are applicable for the gtk# gui.

Check if you can answer one of the following questions with yes!

- Did you program something before in any programming language?
- Did you script something before (like a batch or shell script)?
- Are you persistent and resilient?

If you could answer one of the above questions with "yes" you can continue.

Well this is maybe a bit of overkill but what I'm trying to say is that it will not run instantly.
You have to setup your program in F#!

Even the above statement will sound to you more worse that it actually is. 
But it is true.

## Simple Usage

The first lines are really only there to load the required dependencies and make the Scripting more smooth.

```fsharp
#I @"lib"
#I @"bin\Debug\lib"
#r "Yaaf.SyncLib.dll"
#r "Yaaf.SyncLib.Ui.dll"
open Yaaf.SyncLib
open Yaaf.SyncLib.Ui
open Yaaf.SyncLib.Ui.Scripting
// Add some logging (just leave this line if you don't know what it does
Helpers.AsyncTrace.globalTrace.Listeners.Add(new System.Diagnostics.XmlWriterTraceListener("log.svclog"))
```

You really don't need to know exactly what these lines mean. 
But to give a quick explanation:
- #I will add a directory to the list of searched directories for references
- #r will add a reference to a library
- open will open a namespace or module (very much like using in C# or import in Java)
- The last line will be most likely removed in future versions and sets the logfile

The next lines are more interesting:

```fsharp
// Your startup logic / your folders
let myManagers = [
        Manager Git "MyName" "/home/me/gitrepro" "git@gitserver:repro.git" 
        Manager Svn "SvnRepro" "/home/me/svnrepro" "https://svnserver.com/svn/root"
    ]

// Starting Tray Icon
RunGui myManagers
```

With the [] brackets you basically create a list, in this case you have two entries in this list.
Both entries are "Manager" the first is for Git and the secound for Svn.
Now you can edit this list for as many Git and Svn Repositorys you like to get managed.
For example:

```fsharp
// Your startup logic / your folders
let myManagers = [
        Manager Git "Git1" "/home/me/gitrepro" "git@gitserver:repro.git" 
        Manager Svn "SvnRepro" "/home/me/svnrepro" "https://svnserver.com/svn/root"
        Manager Git "Git2" "/home/me/gitrepro2" "git@gitserver:repro2.git"
        Manager Svn "SvnRepro2" "/home/me/svnrepro2" "https://svnserver2.com/svn/root/folder1"
        Manager Svn "SvnRepro3" "/home/me/svnrepro3" "https://svnserver2.com/svn/root/folder2"
    ]
```
NOTE: Whitespace is important in F#. And Tabs are not allowed. 
So if you don't know anything about F# I would recommend to write every repository on a new line (on one line)
The "let myManagers = ..." part is really only assigning this list to the symbol myManagers.

The last Line:

```fsharp
// Starting Tray Icon
RunGui myManagers
```
Is starting the UI with the given configuration. Thats basically all you need to do.

This API is not finished jet so ...

## Advanced configuration

This section will show you how to give your repros some advanced configuration settings.

TODO: add content.

## Customize Icon

TODO: add content.

## Localize!

All data that is shown was localized with Mono.Posix. So the application can be localized if you want.
Check out http://www.mono-project.com/I18N_with_Mono.Unix. 
If you want you can send your localization back to me and I will add them to the Library.
(This section is not finished).