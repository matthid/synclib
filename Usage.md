# SyncLib 

First of all please make sure if you are applicable for the gtk# gui.

Check if you can answer at least one of the following questions with yes!

- Did you program something before in any programming language?
- Did you script something before (like a batch or shell script)?
- Are you persistent and resilient?

If you could answer one of the above questions with "yes" you can continue. 
(Of couse you can also continue without, this was only to show that it _could_ be frustrating)

You have to setup your program in F#!
Really.

## Simple Usage

First you have to configure the RunApplication.fsx to your needs

### Configuration
The first lines are really only there to load the required dependencies and make the Scripting more smooth.

```fsharp
#I @"lib"
#I @"bin\Debug\lib" // You can remove this line if you want to prevent startup warnings
#r "Yaaf.SyncLib.dll"
#r "Yaaf.SyncLib.Ui.dll"
open Yaaf.SyncLib
open Yaaf.SyncLib.Ui
open Yaaf.SyncLib.Ui.Scripting
```

You really don't need to know exactly what these lines mean. 
But to give a quick explanation:
- #I will add a directory to the list of searched directories for references
- #r will add a reference to a library
- open will open a namespace or module (very much like using in C# or import in Java)

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
So if you don't know anything about F# I would recommend to write every repository on a new line (on one line). And use the corrent intendation!

The "let myManagers = ..." part is really only assigning this list to the symbol myManagers.

The last Line:

```fsharp
// Starting Tray Icon
RunGui myManagers
```
Is starting the UI with the given configuration. Thats basically all you need to do.

This API is not finished jet so be prepared for changes...

### Running

Now if you have your RunApplication.fsx properly configured (or should i say "programmed")
you can run "your" program. 

#### Windows

If you did build your Executables yourself:
Enter the folder src\Yaaf.SyncLib.Ui\bin\Debug and run StartUi.cmd from there.

If you downloaded the binaries:
run "StartUi.cmd" 

#### Linux/possibly Mac

On Linux/Mac you must have a Mono > 2.10.8 installed and in your PATH.
Also make sure you have installed F# (read Readme)

If you did build your Executables yourself:
```bash
cd build/bin 
./StartUi.sh
```

If you downloaded the binaries:
```bash
./StartUi.sh
```

## Advanced configuration

This section will show you how to give your repros some advanced configuration settings.

A basic advanced configuration would look like this
```fsharp
// Your startup logic / your folders
let myManagers = [
        // Edit the following lines to represent your repositories (NOTE: whitespace is important in F#)
        CustomManager 
            Git 
            (BackendInfo 
                "GitReproName" 
                "C:\\users\\me\\documents\\mygitrepro" 
                "git@mygitserver2:repro.git"
                (Map.ofList [("PubsubUrl",     "tcp://notifications.sparkleshare.org:80");
                       ("PubsubChannel", "akhgfjkasbhdfasdf" )]))
		
		// Or this syntax
        CustomManager 
            Git 
            {
                Name = "GitReproName" 
                FullPath = "C:\\users\\me\\documents\\mygitrepro" 
                Remote = "git@mygitserver2:repro.git"
                Additional = Map.ofList [("PubsubUrl",     "tcp://notifications.sparkleshare.org:80");
                                         ("PubsubChannel", "akhgfjkasbhdfasdf" )]
            }
    ]

```
with Map.ofList you can initialize the advanced configuration options, the syntax is:
```fsharp
(Map.ofList [ ( "name1", "value1"); 
			  ("name2","value2"); 
			  // Lots of other values
			  ("nameN","valueN") ])
```
The following values are possible:

<table>
    <tr>
        <td><b>Config-Value</b></td>
        <td><b>Meaning</b></td>
    </tr>
    <tr>
        <td>PubsubUrl</td>
        <td>url to a pubsub server, for example "tcp://notifications.sparkleshare.org:80"</td>
    </tr>
    <tr>
        <td>PubsubChannel</td>
        <td>the channelname for the repro (should be unique and the same for all syncing partners)</td>
    </tr>
	<tr>
		<td>ConflictStrategy</td>
		<td>
1.  KeepLocal means we solve conflicts by keeping the local version and discarding the server version, 
this can work for you on repros with history like git and svn (because you can recover the old version if you need to).
<br />
2.  RenameServer means we rename the server version to a conflict-file
<br />
3.  RenameLocal means we rename the local version to a conflict-file
<br />
4.  Any other value will prevent the startup
<br />
5.  No value means "RenameLocal"
</span>
		</td>
    </tr>
    <tr>
        <td>OfflineRetryDelay</td>
        <td>the delay in secounds (eg "7.5") until we try to reconnect when synclib recognizes that a server is offline, default is "5.0". "0" means we do not try to reconnect.</td>
    </tr>
    <tr>
        <td>gitpath</td>
        <td>lets you setup the path to the git.exe/git. No value means that synclib will try to find a git executable.</td>
    </tr>
    <tr>
        <td>sshpath</td>
        <td>lets you setup the path to a ssh.exe/ssh. No value means that synclib will try to find one. Note: synclib does _not_ force git to use this ssh file.</td>
    </tr>
    <tr>
        <td>svnpath</td>
        <td>lets you setup the path to the svn.exe/svn. No value means that synclib will try to find a svn executable.</td>
    </tr>
</table>

Note that not all values are used by all implementations.

1.  git will _not_ use svnpath
2.  svn will _not_ use gitpath, sshpath, ConflictStrategy (currently it will always do RenameLocal)

There is an additional, more powerfull syntax:

```fsharp
let myManagers = [
        // HARD Syntax
        EmptyManager 
            Git
            { Name = "Repro"; FullPath = "/path/to/repro"; Remote ="git@otherserver:repro"; Additional = Map.empty }
            // Add a filesystemwatcher with a reduced time from 1 minute and ignore the ".git" folder
            |> AddLocalWatcher (System.TimeSpan.FromMinutes 1.0) [".git"]
            // Add a Pubsub server
            |> AddRemoteFromData (Pubsub(new System.Uri("tcp://notifications.sparkleshare.org:80"), "channel"))
	]		
```

## Customize Tray Menu

With
```fsharp
// Starting Tray Icon
RunGui myManagers
```
you can get a very basic tray icon. 
If you look into the "RunGui" you can see how easy it is to configure your menu.
```fsharp
InitGtkGui()
    |> AddManagerIcons managers
    |> AddSuspendManagerIcon managers
    |> AddTriggerSyncButton managers
    |> AddQuitIcon
    |> StartGtk
                
for info, manager in managers do manager.StopService()
```
In this code you can disable or enable specific features/menuitems or reorder them.

```fsharp
InitGtkGui()
    |> AddTriggerSyncButton managers
    |> AddManagerIcons managers
    //|> AddSuspendManagerIcon managers
    |> AddQuitIcon
    |> StartGtk
                
for info, manager in managers do manager.StopService()
```
In this example we have reordered the items and disabled the suspend button.
Actually you can add your own Menuitems, 
but to give them some functionality you should be able to write simple F# code.

NOTE: It is possible to develop your own notification-provider, synchronisation-implementation or menuitems and then just use them in RunApplication.fsx.
You won't even realise a difference.

## Localize!

All data that is shown was localized with Mono.Posix. So the application can be localized if you want.
Check out http://www.mono-project.com/I18N_with_Mono.Unix. 
If you want you can send your localization back to me and I will add them to the Library.
(This section is not finished).