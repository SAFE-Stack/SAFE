module SAFE.Tool

open System

open SAFE.Core

let addPluginWithPaket (plugin : string) =
    let capital = plugin.Substring(0,1).ToUpper() + plugin.Substring(1)
    let paket = Paket.Dependencies.Locate()
    let package = sprintf "SAFE.%s" capital
    let paketGroup = "build"
    printfn "Adding %s package to Paket %s group..."  package paketGroup
    paket.Add(Some paketGroup, package)
    printfn "Package %s added to Paket %s group" package paketGroup

let removePluginWithPaket (plugin : string) =
    let capital = plugin.Substring(0,1).ToUpper() + plugin.Substring(1)
    let paket = Paket.Dependencies.Locate()
    let package = sprintf "SAFE.%s" capital
    let paketGroup = "build"
    printfn "Removing %s package from Paket %s group..."  package paketGroup
    paket.Remove(Some paketGroup, package)
    printfn "Package %s removed from Paket %s group" package paketGroup


[<EntryPoint>]
let main argv =
    match List.ofArray argv with

    
    | [ "add"; plugin ] ->
        if Config.checkPlugin plugin then
            printfn "%s plugin already added" plugin
        else
            addPluginWithPaket plugin
            Config.addPlugin plugin
            printfn "%s plugin added" plugin

    | [ "remove"; plugin ] ->
        if Config.checkPlugin plugin then
            Config.removePlugin plugin
            removePluginWithPaket plugin
            printfn "%s plugin removed" plugin
        else
            printfn "%s plugin not added" plugin

    | [ "build" ] -> 
        System.Diagnostics.Process.Start("fake", sprintf "build -t Build").WaitForExit()

    | [ "build"; plugin ] ->
        if Config.checkPlugin plugin then
            System.Diagnostics.Process.Start("fake", sprintf "build -t Build -- %s" plugin).WaitForExit()
        else
            printfn "%s plugin not added to this project, run `add %s`" plugin plugin

    | [ "run" ] ->
        System.Diagnostics.Process.Start("fake", sprintf "build -t Run").WaitForExit()

    | [ "run"; plugin ] ->
        if Config.checkPlugin plugin then
            System.Diagnostics.Process.Start("fake", sprintf "build -t Run -- %s" plugin).WaitForExit()
        else
            printfn "%s plugin not added to this project, run `add %s`" plugin plugin

    | [ "deploy"; plugin ] ->
        if Config.checkPlugin plugin then
            System.Diagnostics.Process.Start("fake", sprintf "build -t Deploy -- %s" plugin).WaitForExit()
        else
            printfn "%s plugin not added to this project, run `add %s`" plugin plugin

    | _ -> 
        printfn """Usage: safe [command] 
(Available commands: build, run, add, remove)"""

    0 // return an integer exit code
