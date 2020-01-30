module SAFE.Remoting.Server

open Fable.Remoting.Server
open Fable.Remoting.Giraffe

open SAFE.Remoting.Shared

let counterApi = {
    initialCounter = fun () -> async { return { Value = 42 } }
}

let httpHandler: Giraffe.Core.HttpFunc -> Microsoft.AspNetCore.Http.HttpContext -> Giraffe.Core.HttpFuncResult =
    Remoting.createApi()
    |> Remoting.withRouteBuilder routeBuilder
    |> Remoting.fromValue counterApi
    |> Remoting.buildHttpHandler
