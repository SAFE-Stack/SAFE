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

    interface ISAFEPlugin

    interface ISAFEBuildablePlugin with
        member this.Build () = 
            docker ()
