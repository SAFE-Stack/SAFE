// Learn more about F# at http://fsharp.org

open System

open Fake.Core
open Fake.DotNet
open Fake.IO

let serverPath = Path.getFullName "./src/Server"
let clientPath = Path.getFullName "./src/Client"
let clientDeployPath = Path.combine clientPath "deploy"
let deployDir = Path.getFullName "./deploy"

let release = ReleaseNotes.load "RELEASE_NOTES.md"

let platformTool tool winTool =
    let tool = if Environment.isUnix then tool else winTool
    match ProcessUtils.tryFindFileOnPath tool with
    | Some t -> t
    | _ ->
        let errorMsg =
            tool + " was not found in path. " +
            "Please install it and make sure it's available from your path. " +
            "See https://safe-stack.github.io/docs/quickstart/#install-pre-requisites for more info"
        failwith errorMsg

let nodeTool = platformTool "node" "node.exe"
let yarnTool = platformTool "yarn" "yarn.cmd"

let runTool cmd args workingDir =
    let arguments = args |> String.split ' ' |> Arguments.OfArgs
    Command.RawCommand (cmd, arguments)
    |> CreateProcess.fromCommand
    |> CreateProcess.withWorkingDirectory workingDir
    |> CreateProcess.ensureExitCode
    |> Proc.run
    |> ignore

let runDotNet cmd workingDir =
    let result =
        DotNet.exec (DotNet.Options.withWorkingDirectory workingDir) cmd ""
    if result.ExitCode <> 0 then failwithf "'dotnet %s' failed in %s" cmd workingDir

let openBrowser url =
    //https://github.com/dotnet/corefx/issues/10361
    Command.ShellCommand url
    |> CreateProcess.fromCommand
    |> CreateProcess.ensureExitCodeWithMessage "opening browser failed"
    |> Proc.run
    |> ignore


let clean () =
    [ deployDir
      clientDeployPath ]
    |> Shell.cleanDirs

let directory = "."

let installClient () =
    printfn "Node version:"
    runTool nodeTool "--version" directory
    printfn "Yarn version:"
    runTool yarnTool "--version" directory
    runTool yarnTool "install --frozen-lockfile" directory

let build () =
    runDotNet "build" serverPath
    Shell.regexReplaceInFileWithEncoding
        "let app = \".+\""
       ("let app = \"" + release.NugetVersion + "\"")
        System.Text.Encoding.UTF8
        (Path.combine clientPath "Version.fs")
    runTool yarnTool "webpack-cli -p" directory

let run () =
    let server = async {
        runDotNet "watch run" serverPath
    }
    let client = async {
        runTool yarnTool "webpack-dev-server" directory
    }
    let browser = async {
        do! Async.Sleep 5000
        openBrowser "http://localhost:8080"
    }

    let vsCodeSession = Environment.hasEnvironVar "vsCodeSession"
    let safeClientOnly = Environment.hasEnvironVar "safeClientOnly"

    let tasks =
        [ if not safeClientOnly then yield server
          yield client
          if not vsCodeSession then yield browser ]

    tasks
    |> Async.Parallel
    |> Async.RunSynchronously
    |> ignore

type Config =
    { Docker : bool }

module Config =
    open Thoth.Json.Net
    let camelCase = true

    let parse raw =
        Decode.Auto.unsafeFromString<Config> (raw, camelCase)

    let format (config: Config) =
        Encode.Auto.toString(1, config, camelCase)

    let defaultConfig =
        { Docker = false }

    let configDir = "./.config"

    let configFile = Path.combine configDir "safe.json"

    let read () =
        if File.exists configFile then
            File.readAsString configFile
            |> parse
        else
            defaultConfig

    let save config =
        Directory.ensure configDir
        File.writeString false configFile (format config)

    let change f = read () |> f |> save

    let check (f: Config -> bool) = read () |> f

let dockerfileContents = """FROM microsoft/dotnet:2.2-aspnetcore-runtime-alpine
COPY /deploy /
WORKDIR /Server
EXPOSE 8085
ENTRYPOINT [ "dotnet", "Server.dll" ]
"""

let createDockerfile () : IO.FileInfo =
    let tmpPath = IO.Path.GetTempPath()
    let dockerfile = Path.combine tmpPath "SAFE.Tool.Dockerfile"
    if not (File.exists dockerfile) then
        File.writeString false dockerfile dockerfileContents
    IO.FileInfo dockerfile

let addDocker () =
    Config.change (fun x -> { x with Docker = true })

let removeDocker () =
    Config.change (fun x -> { x with Docker = false })

let buildDocker tag =
    let fi = createDockerfile ()
    let args = sprintf "build -f %s -t %s ." fi.FullName tag
    runTool "docker" args "."

let bundle () =
    let serverDir = Path.combine deployDir "Server"
    let clientDir = Path.combine deployDir "Client"
    let publicDir = Path.combine clientDir "public"

    let publishArgs = sprintf "publish -c Release -o \"%s\"" serverDir
    runDotNet publishArgs serverPath

    Shell.copyDir publicDir clientDeployPath FileFilter.allFiles

let dockerUser = "safe-template"
let dockerImageName = "safe-template"
let dockerFullName = sprintf "%s/%s" dockerUser dockerImageName

let docker () =
    buildDocker dockerFullName

let runDocker () =
    let docker = async {
        let args = sprintf "run -it -p 8085:8085 %s" dockerFullName
        runTool "docker" args "."
    }
    let browser = async {
        do! Async.Sleep 5000
        openBrowser "http://localhost:8085"
    }

    let tasks =
        [ docker
          browser ]

    tasks
    |> Async.Parallel
    |> Async.RunSynchronously
    |> ignore

[<EntryPoint>]
let main argv =
    match List.ofArray argv with
    | [ "build" ] -> 
        clean ()
        installClient ()
        build ()
    | [ "run" ] ->
        clean ()
        installClient ()
        run ()
    | [ "add"; "docker" ] ->
        if Config.check (fun c -> c.Docker) then
            printfn "Docker already added"
        else
            addDocker ()
    | [ "remove"; "docker" ] ->
        if Config.check (fun c -> c.Docker) then
            removeDocker ()
        else
            printfn "Docker not added"
    | [ "build"; "docker" ] ->
        if Config.check (fun c -> c.Docker) then
            clean ()
            installClient ()
            build ()
            bundle ()
            docker ()
        else
            printfn "Docker not added to this project, run `add docker`"
    | [ "run"; "docker" ] ->
        if Config.check (fun c -> c.Docker) then
            clean ()
            installClient ()
            build ()
            bundle ()
            docker ()
            runDocker ()
        else
            printfn "Docker not added to this project, run `add docker`"
    | _ -> printfn """Usage: safe [command] 
(Available commands: build, run, add, remove)"""
    0 // return an integer exit code
