namespace SAFE

open System

open Fake.IO

open SAFE.Core

type Docker() =
    inherit SAFEPlugin()

    let buildDocker tag =
        let args = sprintf "build -t %s ." tag
        runTool "docker" args "."

    let dockerUser = "safe-template"
    let dockerImageName = "safe-template"
    let dockerFullName = sprintf "%s/%s" dockerUser dockerImageName

    let docker () =
        buildDocker dockerFullName

    interface ISAFEBuildablePlugin with
        member this.Build () = 
            docker ()
