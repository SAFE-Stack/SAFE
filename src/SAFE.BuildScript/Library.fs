module SAFE.BuildScript

open System

open Fake.IO

let template = sprintf """#r "paket: groupref build //"
#load "./.fake/build.fsx/intellisense.fsx"

#if !FAKE
#r "netstandard"
#r "Facades/netstandard" // https://github.com/ionide/ionide-vscode-fsharp/issues/839#issuecomment-396296095
#endif

open Fake.Core

Target.initEnvironment ()

Target.create "Clean" (fun _ ->
    SAFE.Core.clean ()
)

Target.create "InstallClient" (fun _ ->
    SAFE.Core.installClient ()
)

Target.create "Build" (fun _ ->
    SAFE.Core.build ()
)

Target.create "Bundle" (fun _ ->
    SAFE.Core.bundle ()
)

Target.create "Run" (fun _ ->
    SAFE.Core.run ()
)

%s

open Fake.Core.TargetOperators

"Clean"
    ==> "InstallClient"
    ==> "Build"
    ==> "Bundle"

"Clean"
    ==> "InstallClient"
    ==> "Run"

%s

Target.runOrDefaultWithArguments "Build"
"""

let buildScriptTargets (plugin : string) =
    let capital = plugin.Substring(0,1).ToUpper() + plugin.Substring(1)
    sprintf 
        """Target.create "Build%s" (fun _ ->
    SAFE.%s.Build ()
)

Target.create "Run%s" (fun _ ->
    SAFE.%s.Run ()
)
"""
        capital capital capital capital

let buildScriptOperators (plugin : string) =
    let capital = plugin.Substring(0,1).ToUpper() + plugin.Substring(1)
    sprintf
        """"Bundle" ==> "Build%s"
"Bundle" ==> "Run%s"
"""
        capital capital

let add (runnablePlugins) =
    let targets =
        runnablePlugins
        |> List.map buildScriptTargets
        |> String.concat Environment.NewLine
    let operators =
        runnablePlugins
        |> List.map buildScriptOperators
        |> String.concat Environment.NewLine
    File.writeString false "build.fsx" (template targets operators)

let remove () =
    File.delete "build.fsx"