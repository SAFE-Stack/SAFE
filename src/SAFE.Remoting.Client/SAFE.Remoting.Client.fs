module SAFE.Remoting.Client

open Fable.Remoting.Client

open SAFE.Remoting.Shared

/// A proxy you can use to talk to server directly
let api : ICounterApi =
    Remoting.createApi()
    |> Remoting.withRouteBuilder routeBuilder
    |> Remoting.buildProxy<ICounterApi>
