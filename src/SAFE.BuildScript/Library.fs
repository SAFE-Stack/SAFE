module SAFE.BuildScript

open Fake.IO

let template = """#r "paket: groupref build //"
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

Target.create "Run" (fun _ ->
    SAFE.Core.run ()
)

open Fake.Core.TargetOperators

"Clean"
    ==> "InstallClient"
    ==> "Build"

"Clean"
    ==> "InstallClient"
    ==> "Run"

Target.runOrDefaultWithArguments "Build"
"""

let add () =
    File.writeString false "build.fsx" template
    Config.change (fun x -> { x with BuildScript = true })

let remove () =
    File.delete "build.fsx"
    Config.change (fun x -> { x with BuildScript = false })