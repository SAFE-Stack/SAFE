module SAFE.Remoting.Client

open Fable.Remoting.Client

open SAFE.Remoting.Shared

/// A proxy you can use to talk to server directly
let api : IRemotingApi =
    Remoting.createApi()
    |> Remoting.withRouteBuilder routeBuilder
    |> Remoting.buildProxy<IRemotingApi>

let createCmd msg =
    Elmish.Cmd.OfAsync.perform
        api.getRecord
        ()
        msg