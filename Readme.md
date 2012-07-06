# Yaaf.SyncLib 

SyncLib makes folder syncronisation easy with any piece of technology you like (git, svn, and possibly more).
This is an easy syncronisation library you can use in your code to syncronize some folders.
This library is written entirelyin F# (FSharp) but can be used in any .NET/Mono Language like 
C# (CSharp), F# (FSharp), VB.NET, C++/CLI, Windows Powershell (Could be quite usefull actually), IronPython, IronRuby, IronScheme...

## Dependencies

- https://github.com/forki/FAKE (binaries included, but not jet properly used)
- https://github.com/fsharp/fsharp FSharp libraries required to use SyncLib and fsc required to build the Project
- CLI Runtime
* Mono > 2.10.8 
* .NET 4


## Using

You can either:

- Fork and build the project

- Download and link the binary (whould work on mono-2.10.8 and .net4)

I will add some usage code here but for now just look into the Program.fs of Yaaf.ConsoleSync which is an complete console syncronisation application in 55 lines (pretty neat ;)) 


## Building

This Section will be expended in the future.

For now: fire up Visual Studio and build the Projekt (Windows Only)

I'm working on a FAKE build script (https://github.com/forki/FAKE).
I'd like to have a FAKE build script which will work on linux and windows. 
So if somebody has some insight on this just go ahaid and send a path.


## Contributing

### There are 2 ways to contribute to the project.

- If you plan to send multiple patches in the future the best would be to sign a Contributor-Agreement (ContributorAgreement.md) and send a scanned copy to matthi.d@googlemail.com.

- If you only want to send a single patch (or very few in general) you can state in the comment note and the pull request, that you share your changes under the MIT-License. For example: "This contribution is Licensed unter the http://www.opensource.org/licenses/mit-license.html"

### Why so "complicated"?

I do not consider these above steps as a complication of the contribution process. 
That's what to get for living in a state of law.
I really think free software licenses are the way to go. But the GPL is very restrictive in some terms. Consider these things:

- GPL is only recommend when there is no proprietary äquivalent. This can change in the future and with a pure GPL licensing you can not change.

- I started this project with a lot of effort put into it. Even when the project evolves I would like to have the possibility to use it in other software projects.

- You do not have to be afraid of changing the License of your contribution at will, and you can use your contribution wherever you want (see ContributorAgreement.md). 
* "Any contribution we make available under any license will also be made available under a Free Culture (as defined by http://freedomdefined.org)  or Free Software/Open Source licence (as defined and approved by the Free Software Foundation or the Open Source Initiative)"
* "Except as set out above, you keep all right, title, and interest in your contribution."


## Licensing

This project is subject to the terms and conditions defined in file 'LICENSE.txt', which is part of this source code package. 
License.txt is a GPL License in version 3.

If you require another licensing please write to matthi.d@googlemail.com. (I will always consider helping open source projects).
Also remember you massively contribute to the project I have the option to give you any license you may require.