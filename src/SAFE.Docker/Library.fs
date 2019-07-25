namespace SAFE

open System

open Fake.IO

open SAFE.Core

type Docker() =

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

    let buildDocker tag =
        let fi = createDockerfile ()
        let args = sprintf "build -f %s -t %s ." fi.FullName tag
        runTool "docker" args "."

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

    interface IRunnablePlugin with
        member this.Build () = 
            docker ()

        member this.Run() = 
            docker ()
            runDocker ()
