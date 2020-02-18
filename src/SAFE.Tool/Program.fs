module SAFE.Tool

open System
open System.IO

open Fake.IO.Globbing.Operators

open SAFE.Core

let addPluginWithPaket (plugin : string) =
    let capital = plugin.Substring(0,1).ToUpper() + plugin.Substring(1)
    let paket = Paket.Dependencies.Locate()
    let package = sprintf "SAFE.%s" capital
    let paketGroup = "main"
    printfn "Adding %s package to Paket %s group..."  package paketGroup
    paket.Add(Some paketGroup, package)
    printfn "Package %s added to Paket %s group" package paketGroup
    let contentFiles = !! (sprintf "packages/SAFE.%s/Content/**.*" plugin)
    for file in contentFiles do
        let dest = Path.GetFileName file |> Path.GetFullPath
        printfn "Copying %s to %s" file dest
        File.Copy(file, dest)
    System.Diagnostics.Process.Start("fake", sprintf "build -t PluginCommand -- %s %s" plugin "AfterPluginAdded").WaitForExit()

let removePluginWithPaket (plugin : string) =
    System.Diagnostics.Process.Start("fake", sprintf "build -t PluginCommand -- %s %s" plugin "BeforePluginRemoved").WaitForExit()
    let contentFiles = !! (sprintf "packages/build/SAFE.%s/Content/**.*" plugin)
    for file in contentFiles do
        let path = Path.GetFileName file |> Path.GetFullPath
        printfn "Removing %s" path
        File.Delete(path)
    let capital = plugin.Substring(0,1).ToUpper() + plugin.Substring(1)
    let paket = Paket.Dependencies.Locate()
    let package = sprintf "SAFE.%s" capital
    let paketGroup = "main"
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
    
    | [ plugin; command ] ->
        if Config.checkPlugin plugin then
            System.Diagnostics.Process.Start("fake", sprintf "build -t PluginCommand -- %s %s" plugin command).WaitForExit()
        else
            printfn "%s plugin not added to this project, run `add %s`" plugin plugin

    | [ "test" ] ->
        SAFE.Core.addComponentPlugin "SAFE.Remoting" "Server"

    | _ -> 
        printfn """Usage: safe [command] 
(Available commands: build, run, add, remove)"""

    0 // return an integer exit code
