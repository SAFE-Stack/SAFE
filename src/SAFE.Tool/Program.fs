module SAFE.Tool

open System
open System.Reflection

open SAFE.Core

let bundle () =
    clean ()
    installClient ()
    build ()
    bundle ()

let loadPlugin (plugin : string) =
    let capital = plugin.Substring(0,1).ToUpper() + plugin.Substring(1)
    let assemblyPath = 
        sprintf "packages/build/SAFE.%s/lib/netstandard2.0/SAFE.%s.dll"
            capital
            capital
    let assembly = Assembly.LoadFrom assemblyPath
    let iRunnablePluginType = typeof<ISAFEPlugin>
    assembly.GetTypes()
    |> Array.tryFind iRunnablePluginType.IsAssignableFrom
    |> Option.map (fun typ -> Activator.CreateInstance typ :?> ISAFEPlugin)

[<EntryPoint>]
let main argv =
    match List.ofArray argv with

    | [ "build" ] -> 
        bundle ()

    | [ "run" ] ->
        clean ()
        installClient ()
        run ()
    
    | [ "add"; plugin ] ->
        if Config.checkPlugin plugin then
            printfn "%s plugin already added" plugin
        else
            match loadPlugin plugin with
            | Some p ->
                p.Add (Config.read ())
                Config.addPlugin plugin
                printfn "%s plugin added" plugin
            | None ->
                printfn "%s is not a valid SAFE plugin" plugin

    | [ "remove"; plugin ] ->
        if Config.checkPlugin plugin then
            match loadPlugin plugin with
            | Some p ->
                p.Remove (Config.read ())
                Config.removePlugin plugin
                printfn "%s plugin removed" plugin
            | None ->
                printfn "%s is not a valid SAFE plugin" plugin
        else
            printfn "%s plugin not added" plugin

    | [ "build"; plugin ] ->
        if Config.checkPlugin plugin then
            match loadPlugin plugin with
            | Some (:? ISAFERunnablePlugin as runnable) ->
                bundle ()
                runnable.Build ()
            | Some _ ->
                printfn "%s plugin is not buildable" plugin
            | None ->
                printfn "%s is not a valid SAFE plugin" plugin
        else
            printfn "%s plugin not added to this project, run `add %s`" plugin plugin

    | [ "run"; plugin ] ->
        if Config.checkPlugin plugin then
            match loadPlugin plugin with
            | Some (:? ISAFERunnablePlugin as runnable) ->
                bundle ()
                runnable.Run ()
            | Some _ ->
                printfn "%s plugin is not runnable" plugin
            | None ->
                printfn "%s is not a valid SAFE plugin" plugin
        else
            printfn "%s plugin not added to this project, run `add %s`" plugin plugin

    | _ -> 
        printfn """Usage: safe [command] 
(Available commands: build, run, add, remove)"""

    0 // return an integer exit code
