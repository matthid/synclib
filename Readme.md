# Yaaf.SyncLib 


SyncLib makes folder syncronisation easy with any piece of technology you like (git, svn, and possibly more).
This is an easy syncronisation library you can use in your code to syncronize some folders.
This library is written entirelyin F# (FSharp) but can be used in any .NET/Mono Language like 
C# (CSharp), F# (FSharp), VB.NET, C++/CLI, Windows Powershell (Could be quite usefull actually), IronPython, IronRuby, IronScheme...

This Project contains a console application AND a simple gtk# UI you can change as you like.
The gtk# application will be configured/scripted with the RunApplication.fsx file.

If you want to just use the Gui read: https://github.com/matthid/synclib/blob/master/Usage.md

If you want something more like dropbox (easy install & usage) check out http://sparkleshare.org/. 
SyncLib requires more attention/time in the installing phase. 
But after you configured everything it should run _exactly_ as you want it to run.
If you have any problems open an issue.

SyncLib is still in an early development stage.

## Dependencies

- https://github.com/forki/FAKE (binaries included, but not jet properly used)
- https://github.com/fsharp/fsharp FSharp libraries required to use SyncLib and fsc required to build the Project
- CLI Runtime (one of those)
  * https://github.com/mono/mono Mono > 2.10.8 
  * http://www.microsoft.com/de-de/download/details.aspx?id=17718 .NET 4
- https://github.com/fsharp/powerpack which includes an AsyncStreamReader
- http://nuget.codeplex.com/ not currently used but will be most likly in the future (because of FAKE)
- http://git-scm.com/ Git if you use it
- http://subversion.apache.org/ Svn if you use it

## Using

You can either use the Library or the UI 

### UI

First build (see below) or download (not available quite jet) the binaries then read
https://github.com/matthid/synclib/blob/master/Usage.md

### Library

You can either:

- Fork and build the project

- Download and link the binary (whould work on mono-2.10.8 and .net4)

I will add some usage code here but for now just look into the Program.fs of Yaaf.ConsoleSync which is an complete console syncronisation implementation.
Documentation:
See https://github.com/matthid/synclib/blob/master/Usage.md (basically usage ~ documentation for this library)
Or look into the Scripting.fs which contains a gtk# application using the api.
You can also look into the Yaaf.ConsoleSync application which is a full Console-Application using the library.

In the future you will not have to depend on Yaaf.SyncLib.Git and Yaaf.SyncLib.Svn but for now you have to 
(but only for very few lines, which can be changed easily in the future).

## Building

This Section will be expended in the future.

### Windows

For now: fire up Visual Studio and build the Projekt (Windows Only)

I'm working on a FAKE build script (https://github.com/forki/FAKE).
I'd like to have a FAKE build script which will work on linux and windows. 
So if somebody has some insight on this just go ahaid and send a patch.

### Linux
Building
```bash
git clone git://github.com/matthid/synclib.git
cd synclib
export FSC="mono `pwd`/lib/FSharp-4.0/fsc.exe"
export FSI="mono `pwd`/lib/FSharp-4.0/fsi.exe"
./build_mono.sh
```

## Contributing

### There are 3 ways to contribute to the project.

- If you plan to send multiple patches in the future the best would be to sign a Contributor-Agreement (https://github.com/matthid/synclib/blob/master/ContributorAgreement.md) and send a scanned copy to matthi.d@googlemail.com.

- If you only want to send a single patch (or very few in general) you can state in the comment note and the pull request, that you share your changes under the MIT-License. For example: "This contribution is Licensed unter the http://www.opensource.org/licenses/mit-license.html"

- Report an issue https://github.com/matthid/synclib/issues. See "Report a Bug"

### Why so "complicated"?

I do not consider these above steps as a complication of the contribution process. 
That's what you get for living in a state of law.
I really think free software licenses are the way to go. But the GPL is very restrictive in some ways. Consider these things:

- GPL is only recommend when there is no proprietary äquivalent. This can change in the future and with a pure GPL licensing you can not change.

- I started this project with a lot of effort put into it. Even when the project evolves I would like to have the possibility to use it in other software projects.

- You do not have to be afraid of changing the License of your contribution at will, and you can use your contribution wherever you want (see ContributorAgreement.md). 
  * "Any contribution we make available under any license will also be made available under a Free Culture (as defined by http://freedomdefined.org)  or Free Software/Open Source licence (as defined and approved by the Free Software Foundation or the Open Source Initiative)"
  * "Except as set out above, you keep all right, title, and interest in your contribution."

- If you feal like you can't contribute because of this, please send me a mail or open a issue.

### Report a Bug

If the Bug is an application crash do the following:
- move the contents of the "logs" folder, do not remove/move the logs folder itself.
- start the application and produce the bug.
- attach the contents of the logs folder to your issue request.

If you can't reproduce the bug attach the moved contents.
If it is not a bug you do not have to attach any logs of course. This is true for feature requests, api change requests...

Sometimes even with logs the bug can't be figured out easily. In this case I will request a minidump from you.

TODO: Show how to create a minidump...

## Licensing

This project is subject to the terms and conditions defined in file 'LICENSE.txt', which is part of this source code package. 
https://github.com/matthid/synclib/blob/master/License.txt is a GPL License in version 3.

If you require another licensing please write to matthi.d@googlemail.com. (I will always consider helping open source projects).
Also remember: If you massively contribute to the project I have the option to give you any license you may require.