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
let nupkgDir = Path.getFullName "./nupkg"

let release = ReleaseNotes.load "RELEASE_NOTES.md"
let formattedRN =
    release.Notes
    |> List.map (sprintf "* %s")
    |> String.concat "\n"

Target.create "Clean" (fun _ ->
    Shell.cleanDirs [ nupkgDir ]
)

Target.create "Pack" (fun _ ->
    DotNet.pack
        (fun args ->
            { args with
                    OutputPath = Some nupkgDir
                    Common =
                        { args.Common with
                            CustomParams =
                                Some (sprintf "/p:PackageVersion=%s /p:PackageReleaseNotes=\"%s\""
                                        release.NugetVersion
                                        formattedRN) }
            })
        toolProj
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