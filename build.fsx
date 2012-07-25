// ----------------------------------------------------------------------------
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.
// ----------------------------------------------------------------------------

// BUILD SCRIPT FOR SYNC-LIB
#I @"lib/FAKE"
#r "FakeLib.dll"

open Fake

// properties 
let projectName = "SyncLib"
let projectSummary = "SyncLib - Sync up your files."
let projectDescription = "SyncLib - is a syncronisation library for .NET."
let authors = ["Matthias Dittrich"]
let mail = "bsod@live.de"
let homepage = "https://github.com/matthid"
let buildVersion = "1.0.0.0"

TraceEnvironmentVariables()  
  
let buildDir =      "build" @@ "bin"
let buildLibDir =   "build" @@ "bin" @@ "lib"
let buildLegalDir = "build" @@ "bin" @@ "legal"
let testDir =       "build" @@ "test"
let metricsDir =    "build" @@ "BuildMetrics"
let deployDir =     "build" @@ "Publish" 
let docsDir =       "build" @@ "docs"      
let nugetDir =      "build" @@ "nuget"      
let reportDir =     "build" @@ "report" 
let packagesDir =   "packages"
let deployZip =     deployDir @@ sprintf "%s-%s.zip" projectName buildVersion

// tools
let templatesSrcDir = "lib" @@ "Docu" @@ "templates"
let MSpecVersion = GetPackageVersion packagesDir "Machine.Specifications"
let mspecTool = (sprintf "%sMachine.Specifications.%s" packagesDir MSpecVersion) @@ "tools" @@ "mspec-clr4.exe"

// files

let testReferences = !! ("src" @@ "Yaaf.SyncLibTest**/*.*sproj")
let appReferences  = !! ("src" @@ "Yaaf.SyncLib**/*.*sproj" )

// Targets
Target "Clean" (fun _ ->
    CleanDirs [buildDir; testDir; deployDir; docsDir; metricsDir; nugetDir; reportDir]
)

Target "SetAssemblyInfo" (fun _ ->
    AssemblyInfo 
        (fun p -> 
        {p with
            CodeLanguage = FSharp;
            AssemblyVersion = buildVersion;
            AssemblyTitle = "SyncLib - F# Sync Library";
            Guid = "9D7AA0CB-0512-4F2A-BC5C-A3513763385B";
            OutputFileName = "src" @@ "Yaaf.SyncLib" @@ "AssemblyInfo.fs"})

    AssemblyInfo 
        (fun p -> 
        {p with
            CodeLanguage = FSharp;
            AssemblyVersion = buildVersion;
            AssemblyTitle = "SyncLib.Git - F# Git Bindings for SyncLib";
            Guid = "1341C32A-6D77-496F-9DFD-2B5C9B501C91";
            OutputFileName = "src" @@ "Yaaf.SyncLib.Git" @@ "AssemblyInfo.fs"})
            
    AssemblyInfo 
        (fun p -> 
        {p with
            CodeLanguage = FSharp;
            AssemblyVersion = buildVersion;
            AssemblyTitle = "SyncLib.Svn - F# Svn Bindings for SyncLib";
            Guid = "982D6E42-9FB5-4CC3-83E9-ACCD4A1C8342";
            OutputFileName = "src" @@ "Yaaf.SyncLib.Svn" @@ "AssemblyInfo.fs"})
            
    AssemblyInfo 
        (fun p -> 
        {p with
            CodeLanguage = FSharp;
            AssemblyVersion = buildVersion;
            AssemblyTitle = "SyncLib.Ui - F# Ui and Scripting Bindings for SyncLib";
            Guid = "FD2F60F3-7751-4917-8E2E-F4CB692FEDD7";
            OutputFileName = "src" @@ "Yaaf.SyncLib.Ui" @@ "AssemblyInfo.fs"})
)

Target "BuildApp" (fun _ ->                     
    MSBuildRelease buildLibDir "Build" appReferences
        |> Log "AppBuild-Output: "
    
    (!! (@"lib" @@ "FSharp" @@ "**"))
       |> CopyTo buildDir      

    [@"src" @@ "Yaaf.SyncLib.Ui" @@ "StartUi.cmd"
     @"src" @@ "Yaaf.SyncLib.Ui" @@ "fsi.exe.config"
     @"src" @@ "Yaaf.SyncLib.Ui" @@ "RunApplication.fsx"
     ]
       |> CopyTo buildDir
)

Target "GenerateDocumentation" (fun _ ->
    !! (buildDir + "Yaaf.SyncLib*.dll")
    |> Docu (fun p ->
        {p with
            ToolPath = "lib" @@ "Docu" @@ "docu.exe"
            TemplatesPath = templatesSrcDir
            OutputPath = docsDir })
)

Target "CopyDocu" (fun _ -> 
    (!! ("lib" @@ "Docu" @@ "**"))
       |> CopyTo docsDir
)

Target "CopyLicense" (fun _ -> 
    ["LICENSE.txt"
     "Readme.md"
     "Usage.md"
     "Releasenotes.txt"]
       |> CopyTo buildDir
    System.IO.Directory.CreateDirectory(buildLegalDir) |> ignore
    [@"lib" @@ "FSharp" @@ "FSharp.LICENSE.txt"
     @"lib" @@ "Powerpack" @@ "FSharp.PowerPack.LICENSE.txt"
     @"lib" @@ "Yaaf.AsyncTrace" @@ "Yaaf.AsyncTrace.License.md"
     ]
       |> CopyTo buildLegalDir
)

Target "BuildZip" (fun _ ->     
    !+ (buildDir @@ "**/*.*") 
    -- "*.zip" 
    -- "**/*.pdb"
      |> Scan
      |> Zip buildDir deployZip
)

Target "BuildTest" (fun _ -> 
    MSBuildDebug testDir "Build" testReferences
        |> Log "TestBuild-Output: "
)

Target "Test" (fun _ ->  
    !! (testDir @@ "Test.*.dll") 
      |> MSpec (fun p -> 
            {p with
                ToolPath = mspecTool
                ExcludeTags = ["HTTP"]
                HtmlOutputDir = reportDir}) 
)

Target "ZipDocumentation" (fun _ ->    
    !! (docsDir + @"/**/*.*")  
      |> Zip docsDir (deployDir @@ sprintf "Documentation-%s.zip" buildVersion)
)

Target "CreateNuGet" (fun _ -> 
    let nugetDocsDir = nugetDir @@ "docs"
    let nugetToolsDir = nugetDir @@ "tools"

    XCopy docsDir nugetDocsDir
    XCopy buildDir nugetToolsDir
    DeleteFile (nugetToolsDir @@ "Gallio.dll")

    NuGet (fun p -> 
        {p with               
            Authors = authors
            Project = projectName
            Description = projectDescription                               
            OutputPath = nugetDir
            AccessKey = getBuildParamOrDefault "nugetkey" ""
            Publish = hasBuildParam "nugetkey" }) "fake.nuspec"
)

Target "Default" DoNothing

// Dependencies
"Clean"
    ==> "SetAssemblyInfo"
    ==> "BuildApp" <=> "BuildTest"

    //==> "Test"
    ==> "CopyLicense" <=> "CopyDocu"
    ==> "BuildZip"
    ==> "GenerateDocumentation"
    ==> "ZipDocumentation"
    //==> "CreateNuGet"
    ==> "Default"
  
//if not isLocalBuild then
//    "Clean" ==> "SetAssemblyInfo" ==> "BuildApp" |> ignore

// start build
RunParameterTargetOrDefault "Debug" "Default"



