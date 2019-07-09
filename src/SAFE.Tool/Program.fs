module SAFE.Tool

open SAFE.Core

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
            Docker.addDocker ()
    | [ "remove"; "docker" ] ->
        if Config.check (fun c -> c.Docker) then
            Docker.removeDocker ()
        else
            printfn "Docker not added"
    | [ "build"; "docker" ] ->
        if Config.check (fun c -> c.Docker) then
            clean ()
            installClient ()
            build ()
            Docker.bundle ()
            Docker.docker ()
        else
            printfn "Docker not added to this project, run `add docker`"
    | [ "run"; "docker" ] ->
        if Config.check (fun c -> c.Docker) then
            clean ()
            installClient ()
            build ()
            Docker.bundle ()
            Docker.docker ()
            Docker.runDocker ()
        else
            printfn "Docker not added to this project, run `add docker`"
    | _ -> printfn """Usage: safe [command] 
(Available commands: build, run, add, remove)"""
    0 // return an integer exit code
