namespace SAFE

module Core =
    open Fake.Core
    open Fake.DotNet
    open Fake.IO

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
    let serverPath = Path.getFullName "./src/Server"
    let clientPath = Path.getFullName "./src/Client"
    let clientDeployPath = Path.combine clientPath "deploy"
    let deployDir = Path.getFullName "./deploy"
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

    let bundle () =
        let serverDir = Path.combine deployDir "Server"
        let clientDir = Path.combine deployDir "Client"
        let publicDir = Path.combine clientDir "public"

        let publishArgs = sprintf "publish -c Release -o \"%s\"" serverDir
        runDotNet publishArgs serverPath

        Shell.copyDir publicDir clientDeployPath FileFilter.allFiles

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
    { Plugins : string list }

module Config =
    open Fake.IO

    open Thoth.Json.Net

    let camelCase = true

    let parse raw =
        Decode.Auto.fromString<Config> (raw, camelCase)

    let format (config: Config) =
        Encode.Auto.toString(1, config, camelCase)

    let defaultConfig =
        { Plugins = [] }

    let configDir = "./.config"

    let configFile = Path.combine configDir "safe.json"

    let read () =
        if File.exists configFile then
            match File.readAsString configFile |> parse with
            | Ok c -> c
            | Error _ -> defaultConfig
        else
            defaultConfig

    let save config =
        Directory.ensure configDir
        File.writeString false configFile (format config)

    let change f = read () |> f |> save

    let addPlugin (plugin : string) = 
        change (fun c -> { c with Plugins = plugin :: c.Plugins })

    let removePlugin (plugin : string) =
        change (fun c -> { c with Plugins = c.Plugins |> List.filter ((<>) plugin) })

    let check (f: Config -> bool) = read () |> f

    let checkPlugin (plugin : string) = check (fun c -> c.Plugins |> List.contains plugin)

type ISAFEPlugin =
    abstract member Add : Config -> unit
    abstract member Remove : Config -> unit

type ISAFERunnablePlugin =
    abstract member Build : unit -> unit
    abstract member Run : unit -> unit

type ISAFEDeployablePlugin =
    abstract member Deploy : unit -> unit