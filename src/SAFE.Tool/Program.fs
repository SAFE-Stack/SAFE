module SAFE.Tool

open SAFE.Core

let bundle () =
    clean ()
    installClient ()
    build ()
    bundle ()

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
            Config.addPlugin plugin
            // TODO: dynamic load
            if plugin = "buildScript" then
                // TODO: dynamic check
                let runnablePlugins = ["docker"]
                BuildScript.add (runnablePlugins)
            printfn "%s plugin added" plugin
    
    | [ "remove"; plugin ] ->
        if Config.checkPlugin plugin then
            Config.removePlugin plugin
            // TODO: dynamic load
            if plugin = "buildScript" then
                BuildScript.remove ()
            printfn "%s plugin removed" plugin
        else
            printfn "%s plugin not added" plugin

    | [ "build"; plugin ] ->
        if Config.checkPlugin plugin then
            // TODO: dynamic load
            if plugin = "docker" then
                let runnable = Docker() :> IRunnablePlugin
                bundle ()
                runnable.Build ()
            else
                printfn "%s plugin is not buildable" plugin
        else
            printfn "%s plugin not added to this project, run `add %s`" plugin plugin

    | [ "run"; plugin ] ->
        if Config.checkPlugin plugin then
            // TODO: dynamic load
            if plugin = "docker" then
                let runnable = Docker() :> IRunnablePlugin
                bundle ()
                runnable.Run ()
            else
                printfn "%s plugin is not runnable" plugin
        else
            printfn "%s plugin not added to this project, run `add %s`" plugin plugin

    | _ -> 
        printfn """Usage: safe [command] 
(Available commands: build, run, add, remove)"""

    0 // return an integer exit code
