module SAFE.BuildScript

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

Target.create "Run" (fun _ ->
    SAFE.Core.run ()
)

%s

open Fake.Core.TargetOperators

"Clean"
    ==> "InstallClient"
    ==> "Build"

"Clean"
    ==> "InstallClient"
    ==> "Run"

%s

Target.runOrDefaultWithArguments "Build"
"""

let add (targets, operators) =
    File.writeString false "build.fsx" (template targets operators)

let remove () =
    File.delete "build.fsx"