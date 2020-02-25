#r "paket: groupref build //"
#load "./.fake/build.fsx/intellisense.fsx"

#if !FAKE
#r "netstandard"
#r "Facades/netstandard" // https://github.com/ionide/ionide-vscode-fsharp/issues/839#issuecomment-396296095
#endif

open System

open Fake.Core
open Fake.DotNet
open Fake.IO
open Fake.IO.FileSystemOperators
open Fake.IO.Globbing.Operators
open Fake.Tools

Target.initEnvironment ()

let toolName = "SAFE.Tool"
let toolProj = "./src/SAFE.Tool/SAFE.Tool.fsproj"
let safeProj = "./src/SAFE/SAFE.fsproj"
let clientProj = "./src/SAFE.Client/SAFE.Client.fsproj"
let serverProj = "./src/SAFE.Server/SAFE.Server.fsproj"
let dockerProj = "./src/SAFE.Docker/SAFE.Docker.fsproj"
let herokuProj = "./src/SAFE.Heroku/SAFE.Heroku.fsproj"
let azureAppServiceProj = "./src/SAFE.Azure.AppService/SAFE.Azure.AppService.fsproj"
let projsToPackWithDotnet = 
    [ toolProj
      safeProj
      clientProj
      serverProj
      "./src/SAFE.Remoting/SAFE.Remoting.fsproj" ]
let projsToPackWithPaket =
    [ azureAppServiceProj
      "./src/SAFE.Azure.AppService.Server/SAFE.Azure.AppService.Server.fsproj"
      herokuProj
      dockerProj 
      "./src/SAFE.Remoting.Client/SAFE.Remoting.Client.fsproj"
      "./src/SAFE.Remoting.Server/SAFE.Remoting.Server.fsproj"
      "./src/SAFE.Remoting.Shared/SAFE.Remoting.Shared.fsproj" ]
let toolBin = "./src/SAFE.Tool/bin"

let toolObj = "./src/SAFE.Tool/obj"
let nupkgDir = Path.getFullName "./nupkg"

let release = ReleaseNotes.load "RELEASE_NOTES.md"
let formattedRN =
    release.Notes
    |> List.map (sprintf "* %s")
    |> String.concat "\n"

Target.create "Clean" (fun _ ->
    Shell.cleanDirs [ toolBin; toolObj; nupkgDir ]
)

Target.create "Pack" (fun _ ->
    let customParams = 
        sprintf "/p:PackageVersion=%s /p:PackageReleaseNotes=\"%s\""
            release.NugetVersion
            formattedRN
    for proj in projsToPackWithDotnet do
        DotNet.pack
            (fun args ->
                { args with
                    OutputPath = Some nupkgDir
                    Common = { args.Common with CustomParams = Some customParams }
                })
            proj
    for proj in projsToPackWithPaket do
        DotNet.build
            (fun args ->
                { args with
                    Configuration = DotNet.Release })
            proj
        Paket.pack
            (fun args ->
                { args with
                    OutputPath = nupkgDir
                    WorkingDir = proj |> Path.getDirectory
                    ToolPath = "paket"
                    Version = release.NugetVersion
                })
)

Target.create "Install" (fun _ ->
    let args = sprintf "uninstall -g %s" toolName
    let result = DotNet.exec (fun x -> { x with DotNetCliPath = "dotnet" }) "tool" args
    if not result.OK then failwithf "`dotnet %s` failed with %O" args result
    let args = sprintf "install -g --add-source \"%s\" %s" nupkgDir toolName
    let result = DotNet.exec (fun x -> { x with DotNetCliPath = "dotnet" }) "tool" args
    if not result.OK then failwithf "`dotnet %s` failed with %O" args result
)

Target.create "Push" (fun _ ->
    Paket.push ( fun args ->
        { args with
                PublishUrl = "https://www.nuget.org"
                WorkingDir = nupkgDir
                ToolPath = "paket"
        }
    )

    let remoteGit = "upstream"
    let commitMsg = sprintf "Bumping version to %O" release.NugetVersion
    let tagName = string release.NugetVersion

    Git.Branches.checkout "" false "master"
    Git.CommandHelper.directRunGitCommand "" "fetch origin" |> ignore
    Git.CommandHelper.directRunGitCommand "" "fetch origin --tags" |> ignore

    Git.Staging.stageAll ""
    Git.Commit.exec "" commitMsg
    Git.Branches.pushBranch "" remoteGit "master"

    Git.Branches.tag "" tagName
    Git.Branches.pushTag "" remoteGit tagName
)

Target.create "Release" ignore

open Fake.Core.TargetOperators

"Clean"
    ==> "Pack"
    ==> "Install"
    ==> "Push"
    ==> "Release"

Target.runOrDefaultWithArguments "Install"