// ----------------------------------------------------------------------------
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.
// ----------------------------------------------------------------------------

// BUILD SCRIPT FOR SYNC-LIB
#I @"lib\FAKE"
#r "FakeLib.dll"

open Fake

// properties 
let projectName = "SyncLib"
let projectSummary = "SyncLib - Sync up your files."
let projectDescription = "SyncLib - is a syncronisation library for .NET."
let authors = ["Matthias Dittrich"]
let mail = "bsod@live.de"
let homepage = "https://github.com/matthid"

TraceEnvironmentVariables()  
  
let buildDir = @".\build\bin"
let testDir = @".\build\test\"
let metricsDir = @".\build\BuildMetrics\"
let deployDir = @".\build\Publish\"
let docsDir = @".\build\docs\" 
let nugetDir = @".\build\nuget\" 
let reportDir = @".\build\report\" 
let deployZip = deployDir @@ sprintf "%s-%s.zip" projectName buildVersion
let packagesDir = @".\packages\"

// tools
let templatesSrcDir = @".\lib\Docu\templates\"
let MSpecVersion = GetPackageVersion packagesDir "Machine.Specifications"
let mspecTool = sprintf @"%sMachine.Specifications.%s\tools\mspec-clr4.exe" packagesDir MSpecVersion

// files
let appReferences  = !! @"src\SyncLib**\*.*sproj"
let testReferences = !! @"src\TestSyncLib**\*.*sproj"

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
            OutputFileName = @".\src\SyncLib\AssemblyInfo.fs"})

    AssemblyInfo 
        (fun p -> 
        {p with
            CodeLanguage = FSharp;
            AssemblyVersion = buildVersion;
            AssemblyTitle = "SyncLib.Git - F# Git Bindings for SyncLib";
            Guid = "1341C32A-6D77-496F-9DFD-2B5C9B501C91";
            OutputFileName = @".\src\SyncLib.Git\AssemblyInfo.fs"})
)

Target "BuildApp" (fun _ ->                     
    MSBuildRelease buildDir "Build" appReferences
        |> Log "AppBuild-Output: "
)

Target "GenerateDocumentation" (fun _ ->
    !! (buildDir + "Fake*.dll")
    |> Docu (fun p ->
        {p with
            ToolPath = buildDir @@ "docu.exe"
            TemplatesPath = templatesSrcDir
            OutputPath = docsDir })
)

Target "CopyDocu" (fun _ -> 
    ["./lib/Docu/docu.exe"
     "./lib/Docu/DocuLicense.txt" 
     "./lib/Docu/templates*"]
       |> CopyTo buildDir
)

Target "CopyLicense" (fun _ -> 
    ["License.txt"
     "readme.markdown"
     "changelog.markdown"]
       |> CopyTo buildDir
)

Target "BuildZip" (fun _ ->     
    !+ (buildDir + @"\**\*.*") 
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
    !! (docsDir + @"\**\*.*")  
      |> Zip docsDir (deployDir @@ sprintf "Documentation-%s.zip" buildVersion)
)

Target "CreateNuGet" (fun _ -> 
    let nugetDocsDir = nugetDir @@ "docs/"
    let nugetToolsDir = nugetDir @@ "tools/"

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
    ==> "BuildApp" <=> "BuildTest"
    ==> "Test"
    ==> "CopyLicense" <=> "CopyDocu"
    ==> "BuildZip"
    ==> "GenerateDocumentation"
    ==> "ZipDocumentation"
    ==> "CreateNuGet"
    ==> "Default"
  
if not isLocalBuild then
    "Clean" ==> "SetAssemblyInfo" ==> "BuildApp" |> ignore

// start build
RunParameterTargetOrDefault "target" "Default"



