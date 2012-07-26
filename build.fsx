#I "./packages/FAKE.1.64.6/tools"
#r "FakeLib.dll"

open Fake
open System.IO

// properties
let projectName = "Frack"
let version = if isLocalBuild then "0.1." + System.DateTime.UtcNow.ToString("yMMdd") else buildVersion
let projectSummary = "Frack is a F# based socket implementation for high-speed, high-throughput applications."
let projectDescription = "Frack is an F# based socket implementation for high-speed, high-throughput applications. It is built on top of SocketAsyncEventArgs, which minimises the memory fragmentation common in the IAsyncResult pattern."
let authors = ["Dave Thomas";"Ryan Riley"]
let mail = "ryan.riley@panesofglass.org"
let homepage = "http://github.com/panesofglass/frack"
let license = "http://github.com/panesofglass/frack/raw/master/LICENSE.txt"

// directories
let buildDir = "./build/"
let packagesDir = "./packages/"
let testDir = "./test/"
let deployDir = "./deploy/"
let docsDir = "./docs/"

let targetPlatformDir = getTargetPlatformDir "4.0.30319"

let nugetDir = "./nuget/"
let nugetLibDir = nugetDir @@ "lib/net40"
let nugetDocsDir = nugetDir @@ "docs"

let fsharpxVersion = GetPackageVersion packagesDir "FSharpx.Core"

// params
let target = getBuildParamOrDefault "target" "All"

// tools
let fakePath = "./packages/FAKE.1.64.6/tools"
let nugetPath = "./.nuget/nuget.exe"
let nunitPath = "./packages/NUnit.Runners.2.6.0.12051/tools"

// files
let appReferences =
    !+ "./src/**/*.fsproj"
        |> Scan

let testReferences =
    !+ "./tests/**/*.fsproj"
      |> Scan

let filesToZip =
    !+ (buildDir + "/**/*.*")
        -- "*.zip"
        |> Scan

// targets
Target "Clean" (fun _ ->
    CleanDirs [buildDir; testDir; deployDir; docsDir]
)

Target "BuildApp" (fun _ ->
    AssemblyInfo (fun p ->
        {p with 
            CodeLanguage = FSharp
            AssemblyVersion = version
            AssemblyTitle = projectSummary
            AssemblyDescription = projectDescription
            Guid = "020697d7-24a3-4ce4-a326-d2c7c204ffde"
            OutputFileName = "./src/Frack/AssemblyInfo.fs" })

    MSBuildRelease buildDir "Build" appReferences
        |> Log "AppBuild-Output: "
)

Target "BuildTest" (fun _ ->
    MSBuildDebug testDir "Build" testReferences
        |> Log "TestBuild-Output: "
)

Target "Test" (fun _ ->
    !+ (testDir + "/*.Tests.dll")
        |> Scan
        |> NUnit (fun p ->
            {p with
                ToolPath = nunitPath
                DisableShadowCopy = true
                OutputFile = testDir + "TestResults.xml" })
)

Target "GenerateDocumentation" (fun _ ->
    !+ (buildDir + "*.dll")
        |> Scan
        |> Docu (fun p ->
            {p with
                ToolPath = fakePath + "/docu.exe"
                TemplatesPath = "./lib/templates"
                OutputPath = docsDir })
)

Target "CopyLicense" (fun _ ->
    [ "LICENSE.txt" ] |> CopyTo buildDir
)

Target "ZipDocumentation" (fun _ ->
    !+ (docsDir + "/**/*.*")
        |> Scan
        |> Zip docsDir (deployDir + sprintf "Documentation-%s.zip" version)
)

Target "BuildNuGet" (fun _ ->
    CleanDirs [nugetDir; nugetLibDir; nugetDocsDir]

    XCopy (docsDir |> FullName) nugetDocsDir
    [ buildDir + "Frack.dll"
      buildDir + "Frack.pdb" ]
        |> CopyTo nugetLibDir

    NuGet (fun p -> 
        {p with               
            Authors = authors
            Project = projectName
            Description = projectDescription
            Version = version
            OutputPath = nugetDir
            Dependencies = ["FSharpx.Core",RequireExactly fsharpxVersion]
            AccessKey = getBuildParamOrDefault "nugetkey" ""
            ToolPath = nugetPath
            Publish = hasBuildParam "nugetKey" })
        "frack.nuspec"

    [nugetDir + sprintf "Frack.%s.nupkg" version]
        |> CopyTo deployDir
)

Target "Deploy" (fun _ ->
    !+ (buildDir + "/**/*.*")
        -- "*.zip"
        |> Scan
        |> Zip buildDir (deployDir + sprintf "%s-%s.zip" projectName version)
)

Target "All" DoNothing

// Build order
"Clean"
  ==> "BuildApp" <=> "CopyLicense"
//  ==> "GenerateDocumentation"
//  ==> "ZipDocumentation"
  ==> "BuildNuGet"
  ==> "Deploy"

"All" <== ["Deploy"]

// Start build
Run target

