// Learn more about F# at http://fsharp.org

open System
open System.Diagnostics

let clean () =
    ()

let build (target: string option) =
    let args =
        match target with
        | Some target -> sprintf "build -t %s" target
        | None -> "build"
    let info = ProcessStartInfo("fake", args)
    let proc = Process.Start(info)
    proc.WaitForExit()

[<EntryPoint>]
let main argv =
    match List.ofArray argv with
    | [ "build" ] -> build None
    | [ "run" ] -> build (Some "run")
    | _ -> printfn """Usage: safe [command] 
(Available commands: build, run)"""
    0 // return an integer exit code
