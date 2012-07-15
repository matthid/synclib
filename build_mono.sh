#!/bin/bash
cd src/Yaaf.SyncLib
rm -R bin
rm -R obj
mkdir -p obj/Debug
mkdir -p obj/Release
mkdir -p bin/Debug
mkdir -p bin/Release
$FSC -o:obj/Debug/Yaaf.SyncLib.dll -g --debug:full --noframework --define:DEBUG --define:TRACE --doc:bin/Debug/SparkleLib.FSharp.XML --optimize- --tailcalls- -r:/home/reddragon/programs/FSharp/4.0/lib/mono/4.0/FSharp.Core.dll -r:../../lib/Powerpack/FSharp.PowerPack.dll -r:/usr/lib/mono/4.0/mscorlib.dll -r:/usr/lib/mono/4.0/System.Core.dll -r:/usr/lib/mono/4.0/System.dll -r:/usr/lib/mono/4.0/System.Numerics.dll -r:/usr/lib/mono/4.0/System.Xml.dll -r:../../lib/Yaaf.AsyncTrace/Yaaf.AsyncTrace.dll --target:library --warn:3 --warnaserror:76 --vserrors --LCID:1031 --utf8output --fullpaths --flaterrors AssemblyInfo.fs Helpers.fs AsyncStreamWriter.fs PubsubImplementation.fs ManagedFolder.fs ChangeWatcher.fs ToolProcess.fs RepositoryFolder.fs 
cp obj/Debug/Yaaf.SyncLib.dll bin/Debug/
cd ../Yaaf.SyncLib.Svn
rm -R bin
rm -R obj
mkdir -p obj/Debug
mkdir -p obj/Release
mkdir -p bin/Debug
mkdir -p bin/Release

$FSC -o:obj/Debug/Yaaf.SyncLib.Svn.dll -g --debug:full --noframework --define:DEBUG --define:TRACE --doc:bin/Debug/SyncLib.Svn.XML --optimize- --tailcalls- -r:"../../lib/FSharp-4.0/FSharp.Core.dll" -r:"/usr/lib/mono/4.0/mscorlib.dll" -r:"/usr/lib/mono/4.0/System.Core.dll" -r:"/usr/lib/mono/4.0/System.dll" -r:"/usr/lib/mono/4.0/System.Numerics.dll" -r:../../lib/Yaaf.AsyncTrace/Yaaf.AsyncTrace.dll -r:../../src/Yaaf.SyncLib/bin/Debug/Yaaf.SyncLib.dll --target:library --warn:3 --warnaserror:76 --vserrors --LCID:1031 --utf8output --fullpaths --flaterrors SvnProcess.fs SvnRepositoryFolder.fs SvnBackendManager.fs 
cp obj/Debug/Yaaf.SyncLib.Svn.dll bin/Debug/
cd ../Yaaf.SyncLib.Git
rm -R bin
rm -R obj
mkdir -p obj/Debug
mkdir -p obj/Release
mkdir -p bin/Debug
mkdir -p bin/Release

$FSC -o:obj/Debug/Yaaf.SyncLib.Git.dll -g --debug:full --noframework --define:DEBUG --define:TRACE --doc:bin/Debug/SparkleLib.Git.FSparp.XML --optimize- --tailcalls- -r:"../../lib/FSharp-4.0/FSharp.Core.dll" -r:"/usr/lib/mono/4.0/mscorlib.dll" -r:"/usr/lib/mono/4.0/System.Core.dll" -r:"/usr/lib/mono/4.0/System.dll" -r:"/usr/lib/mono/4.0/System.Numerics.dll" -r:../../lib/Yaaf.AsyncTrace/Yaaf.AsyncTrace.dll -r:../../src/Yaaf.SyncLib/bin/Debug/Yaaf.SyncLib.dll --target:library --warn:3 --warnaserror:76 --vserrors --LCID:1031 --utf8output --fullpaths --flaterrors AssemblyInfo.fs SshProcess.fs GitProcess.fs GitRepositoryFolder.fs GitBackendManager.fs 
cp obj/Debug/Yaaf.SyncLib.Git.dll bin/Debug/
cd ../Yaaf.SyncLib.Ui
rm -R bin
rm -R obj
mkdir -p obj/Debug
mkdir -p obj/Release
mkdir -p bin/Debug
mkdir -p bin/Release

$FSC -o:obj/Debug/Yaaf.SyncLib.Ui.dll -g --debug:full --noframework --define:DEBUG --define:TRACE --doc:bin/Debug/Yaaf.SyncLib.Ui.XML --optimize- --tailcalls- -r:../../lib/Yaaf.AsyncTrace/Yaaf.AsyncTrace.dll -r:/usr/lib/mono/gtk-sharp-2.0/atk-sharp.dll -r:"../../lib/FSharp-4.0/FSharp.Core.dll" -r:"/usr/lib/mono/gtk-sharp-2.0/gdk-sharp.dll" -r:"/usr/lib/mono/gtk-sharp-2.0/glade-sharp.dll" -r:"/usr/lib/mono/gtk-sharp-2.0/glib-sharp.dll" -r:"/usr/lib/mono/gtk-sharp-2.0/gtk-sharp.dll" -r:"/usr/lib/mono/4.0/Mono.Cairo.dll" -r:"/usr/lib/mono/4.0/Mono.Posix.dll" -r:"/usr/lib/mono/4.0/mscorlib.dll" -r:"/usr/lib/mono/gtk-sharp-2.0/pango-sharp.dll" -r:"/usr/lib/mono/4.0/System.Configuration.dll" -r:"/usr/lib/mono/4.0/System.Core.dll" -r:"/usr/lib/mono/4.0/System.dll" -r:"/usr/lib/mono/4.0/System.Numerics.dll" -r:../../src/Yaaf.SyncLib/bin/Debug/Yaaf.SyncLib.dll -r:../../src/Yaaf.SyncLib.Git/bin/Debug/Yaaf.SyncLib.Git.dll -r:../../src/Yaaf.SyncLib.Svn/bin/Debug/Yaaf.SyncLib.Svn.dll --target:library --warn:3 --warnaserror:76 --vserrors --LCID:1031 --utf8output --fullpaths --flaterrors GtkUtils.fs PosixHelper.fs GtkNotification.fs Scripting.fs
cp obj/Debug/Yaaf.SyncLib.Ui.dll bin/Debug
cp RunApplication.fsx bin/Debug
cp StartUi.cmd bin/Debug
cd ../Yaaf.ConsoleSync
rm -R bin
rm -R obj
mkdir -p obj/Debug
mkdir -p obj/Release
mkdir -p bin/Debug
mkdir -p bin/Release

$FSC -o:obj/Debug/Yaaf.ConsoleSync.exe -g --debug:full --noframework --define:DEBUG --define:TRACE --doc:bin/Debug/ConsoleSync.XML --optimize- --tailcalls- --platform:x86 -r:"../../lib/FSharp-4.0/FSharp.Core.dll" -r:"/usr/lib/mono/4.0/mscorlib.dll" -r:"/usr/lib/mono/4.0/System.Core.dll" -r:"/usr/lib/mono/4.0/System.dll" -r:"/usr/lib/mono/4.0/System.Numerics.dll" -r:../../src/Yaaf.SyncLib/bin/Debug/Yaaf.SyncLib.dll -r:../../src/Yaaf.SyncLib.Git/bin/Debug/Yaaf.SyncLib.Git.dll -r:../../src/Yaaf.SyncLib.Svn/bin/Debug/Yaaf.SyncLib.Svn.dll -r:../../src/Yaaf.SyncLib.Ui/bin/Debug/Yaaf.SyncLib.Ui.dll --target:exe --warn:3 --warnaserror:76 --vserrors --LCID:1031 --utf8output --fullpaths --flaterrors BackendTester.fs Program.fs 
cp obj/Debug/Yaaf.ConsoleSync.exe bin/Debug
cd ../..

mkdir -p build/bin/lib/
mkdir -p build/bin/logs
cp src/Yaaf.SyncLib/bin/Debug/Yaaf.SyncLib.dll build/bin/lib/
cp src/Yaaf.SyncLib.Svn/bin/Debug/Yaaf.SyncLib.Svn.dll build/bin/lib/
cp src/Yaaf.SyncLib.Git/bin/Debug/Yaaf.SyncLib.Git.dll build/bin/lib/
cp src/Yaaf.SyncLib.Ui/bin/Debug/Yaaf.SyncLib.Ui.dll build/bin/lib/
cp src/Yaaf.SyncLib.Ui/RunApplication.fsx build/bin/
cp src/Yaaf.SyncLib.Ui/StartUi.cmd build/bin/
cp src/Yaaf.ConsoleSync/bin/Debug/Yaaf.ConsoleSync.exe build/bin/lib/

cp lib/Powerpack/FSharp.PowerPack.dll build/bin/lib/
cp lib/Yaaf.AsyncTrace/Yaaf.AsyncTrace.dll build/bin/lib/
cp lib/FSharp-4.0/* build/bin/
cp src/Yaaf.SyncLib.Ui/fsi.exe.config build/bin/

