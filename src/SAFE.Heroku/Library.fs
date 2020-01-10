namespace SAFE.Heroku

open Fake.IO

open SAFE
open SAFE.Core

type Heroku () =
    inherit SAFEPlugin()

    let sourceDir = "."

    let configure () =
        let gitTool = platformTool "git" "git.exe"
        let herokuTool = platformTool "heroku" "heroku.cmd"
        let arguments =  "apps:create"
        let output = runToolWithOutput herokuTool arguments sourceDir
        let app = (output.Split '|').[0]
        printfn "app created in %s" (app.Trim())
        let appName = app.[8..(app.IndexOf(".")-1)]
        runTool gitTool "init" sourceDir
        let gitCmd = sprintf "git:remote --app %s" appName
        runTool herokuTool gitCmd sourceDir
        runTool herokuTool "buildpacks:set -i 1 https://github.com/heroku/heroku-buildpack-nodejs" sourceDir
        runTool herokuTool "buildpacks:set -i 2 https://github.com/SAFE-Stack/SAFE-buildpack" sourceDir
        runTool gitTool "add ." sourceDir
        runTool gitTool "commit -m initial" sourceDir

    let deploy () =
        let gitTool = platformTool "git" "git.exe"
        runTool gitTool "push heroku master" sourceDir
        let herokuTool = platformTool "heroku" "heroku.cmd"
        runTool herokuTool "open" sourceDir

    override __.AfterPluginAdded() =
        base.AfterPluginAdded()
        configure ()

    interface ISAFEDeployablePlugin with
        member this.Deploy () =
            deploy()