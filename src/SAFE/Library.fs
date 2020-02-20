namespace SAFE

[<AbstractClass>]
type SAFEPlugin() = 
    abstract member Snippets: list<string * list<string * string>>
    abstract member AfterPluginAdded: unit -> unit
    abstract member BeforePluginRemoved: unit -> unit

    default __.AfterPluginAdded () = ()
    default __.BeforePluginRemoved () = ()
    default __.Snippets = []

type ISAFEClientPlugin = interface end
type ISAFEServerPlugin = interface end
type ISAFESharedPlugin = interface end

type ISAFEBuildablePlugin =
    abstract member Build : unit -> unit

type ISAFEDeployablePlugin =
    abstract member Deploy : unit -> unit

module Core =
    open System.IO

    open Fake.Core
    open Fake.DotNet
    open Fake.IO
    open Fake.IO.Globbing.Operators

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

    let runToolWithOutput cmd args workingDir =
        let arguments = args |> String.split ' ' |> Arguments.OfArgs
        let result =
            Command.RawCommand (cmd, arguments)
            |> CreateProcess.fromCommand
            |> CreateProcess.withWorkingDirectory workingDir
            |> CreateProcess.ensureExitCode
            |> CreateProcess.redirectOutput
            |> Proc.run
        result.Result.Output |> (fun s -> s.TrimEnd())

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

    let clean (p: Fake.Core.TargetParameter) =
        [ deployDir
          clientDeployPath ]
        |> Shell.cleanDirs

    let directory = "."

    let installClient (p: Fake.Core.TargetParameter) =
        printfn "Node version:"
        runTool nodeTool "--version" directory
        printfn "Yarn version:"
        runTool yarnTool "--version" directory
        runTool yarnTool "install --frozen-lockfile" directory

    let getPlugin (p: Fake.Core.TargetParameter) =
        try
            let plugin = p.Context.Arguments |> List.head
            let capital = plugin.Substring(0,1).ToUpper() + plugin.Substring(1)
            sprintf "SAFE.%s" capital |> Some
        with _ -> None

    let getCommand (p: Fake.Core.TargetParameter) =
        try
            let plugin = p.Context.Arguments |> List.tail |> List.head
            let capital = plugin.Substring(0,1).ToUpper() + plugin.Substring(1)
            Some capital
        with _ -> None

    let loadPlugin<'a> (name: string) : 'a option =
        printfn "--> loading %s" name
        let assembly = System.Reflection.Assembly.Load name
        let pluginType = typeof<'a>
        assembly.GetTypes()
        |> Array.tryFind pluginType.IsAssignableFrom
        |> Option.map (fun typ -> System.Activator.CreateInstance typ :?> 'a)

    let build (p: Fake.Core.TargetParameter) =
        match getPlugin p with
        | Some name ->
            match loadPlugin<ISAFEBuildablePlugin> name with
            | Some p -> p.Build()
            | None -> printfn "Not runnable!"
        | None ->
            runDotNet "build" serverPath
            Shell.regexReplaceInFileWithEncoding
                "let app = \".+\""
               ("let app = \"" + release.NugetVersion + "\"")
                System.Text.Encoding.UTF8
                (Path.combine clientPath "Version.fs")
            runTool yarnTool "webpack-cli -p" directory

    let bundle (p: Fake.Core.TargetParameter) =
        let serverDir = Path.combine deployDir "Server"
        let clientDir = Path.combine deployDir "Client"
        let publicDir = Path.combine clientDir "public"

        let publishArgs = sprintf "publish -c Release -o \"%s\"" serverDir
        runDotNet publishArgs serverPath

        Shell.copyDir publicDir clientDeployPath FileFilter.allFiles

    let run (p: Fake.Core.TargetParameter) =
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

    let deploy (p: Fake.Core.TargetParameter) =
        match getPlugin p with
        | Some name ->
            match loadPlugin<ISAFEDeployablePlugin> name with
            | Some p -> p.Deploy ()
            | None -> printfn "Not deployable!"
        | None ->
            printfn "Deploy must be invoked for a plugin!"

    open System.Xml.Linq
    open System.Xml.XPath

    let addContentFiles plugin _component =
        let contentFiles = !! (sprintf "packages/%s.%s/Content/**.*" plugin _component)
        for file in contentFiles do
            let projDir = Path.combine "src" _component
            let fsprojPath = Path.combine projDir (_component + ".fsproj")
            let dest = Path.combine projDir (Path.GetFileName file)
            printfn "Copying %s to %s" file dest
            File.Copy(file, dest)
            printfn "Adding %s to %s" fsprojPath (Path.GetFileName file)
            let xdoc = XDocument.Load fsprojPath 
            let xn = XName.op_Implicit
            let node = XElement(xn "Compile", XAttribute(xn "Include", Path.GetFileName file))
            let lastCompileNode = xdoc.XPathSelectElements "//Compile" |> Seq.last
            lastCompileNode.AddBeforeSelf node
            xdoc.Save fsprojPath
    
    let removeContentFiles plugin _component =
        let contentFiles = !! (sprintf "packages/%s.%s/Content/**.*" plugin _component)
        for file in contentFiles do
            let projDir = Path.combine "src" _component
            let fsprojPath = Path.combine projDir (_component + ".fsproj")
            let dest = Path.combine projDir (Path.GetFileName file)
            printfn "Removing %s from %s" dest fsprojPath
            let xdoc = XDocument.Load fsprojPath 
            let xn = XName.op_Implicit
            let node = xdoc.XPathSelectElements (sprintf "//Compile[@Include='%s']" (Path.GetFileName file)) |> Seq.head
            node.Remove()
            xdoc.Save fsprojPath
            printfn "Removing %s" dest
            File.Delete(dest)

    let addComponentPlugin (plugin : string) _component =
        let capital = plugin.Substring(0,1).ToUpper() + plugin.Substring(1)
        let paket = Paket.Dependencies.Locate()
        let package = sprintf "%s.%s" capital _component
        let paketGroup = "main"
        printfn "Adding %s package to Paket %s group..."  package paketGroup
        paket.AddToProject(Some paketGroup, package, "", false, false, false, false, sprintf "src/%s/%s.fsproj" _component _component, true, Paket.SemVerUpdateMode.NoRestriction, false)
        printfn "Package %s added to Paket %s group" package paketGroup
        addContentFiles capital _component

    let removeComponentPlugin (plugin : string) _component =
        let capital = plugin.Substring(0,1).ToUpper() + plugin.Substring(1)
        let paket = Paket.Dependencies.Locate()
        let package = sprintf "%s.%s" capital _component
        removeContentFiles capital _component
        paket.Remove package

    let addSnippets(p: SAFEPlugin) =
        for (file, snippets) in p.Snippets do
            let lines = System.IO.File.ReadAllLines file |> ResizeArray
            for (regex, snippet) in snippets do
                printf "File '%s': line matching regex /%s/: " file regex
                try
                    let regex = System.Text.RegularExpressions.Regex regex
                    match Seq.tryFindIndex regex.IsMatch lines with
                    | None -> printfn "not found!"
                    | Some lineNo -> 
                        printfn "%d" lineNo
                        printfn "Inserting following snippet after: '%s'" snippet
                        lines.Insert(lineNo + 1, snippet)
                with e ->
                    printfn "Exception: %O" e
            printfn "Saving file '%s'" file
            System.IO.File.WriteAllLines (file, lines)

    let removeSnippets(p: SAFEPlugin) =
        for (file, snippets) in p.Snippets do
            let lines = System.IO.File.ReadAllLines file |> ResizeArray
            for (_, snippet) in snippets do
                printf "File '%s': line with snippet '%s': " file snippet
                try
                    match Seq.tryFindIndex ((=) snippet) lines with
                    | None -> printfn "not found!"
                    | Some lineNo -> 
                        printfn "%d" lineNo
                        printfn "Deleting the line"
                        lines.RemoveAt(lineNo)
                with e ->
                    printfn "Exception: %O" e
            printfn "Saving file '%s'" file
            System.IO.File.WriteAllLines (file, lines)

    let pluginCommand (p: Fake.Core.TargetParameter) =
        match getPlugin p, getCommand p with
        | Some name, Some methodName ->
            match loadPlugin<SAFEPlugin> name with
            | Some p -> 
                match methodName with
                | "AfterPluginAdded" -> 
                    if typeof<ISAFESharedPlugin>.IsAssignableFrom (p.GetType()) then
                        addComponentPlugin name "Shared"
                    if typeof<ISAFEClientPlugin>.IsAssignableFrom (p.GetType()) then
                        addComponentPlugin name "Client"
                    if typeof<ISAFEServerPlugin>.IsAssignableFrom (p.GetType()) then
                        addComponentPlugin name "Server"
                    addSnippets p
                    p.AfterPluginAdded()
                | "BeforePluginRemoved" -> 
                    removeSnippets p
                    p.BeforePluginRemoved()
                    if typeof<ISAFESharedPlugin>.IsAssignableFrom (p.GetType()) then
                        removeComponentPlugin name "Shared"
                    if typeof<ISAFEClientPlugin>.IsAssignableFrom (p.GetType()) then
                        removeComponentPlugin name "Client"
                    if typeof<ISAFEServerPlugin>.IsAssignableFrom (p.GetType()) then
                        removeComponentPlugin name "Server"
                | _ ->
                    let typ = p.GetType()
                    let method = typ.GetMethod methodName
                    if method <> null then
                        method.Invoke(p, null) |> ignore
                    else
                        printfn "No %s method found for %s plugin" methodName name
            | None -> printfn "Not a plugin!"
        | _ ->
            printfn "PluginCommand must be invoked for a plugin and command!"

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
