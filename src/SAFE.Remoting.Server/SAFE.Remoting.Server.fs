module SAFE.Remoting.Server

open Fable.Remoting.Server
open Fable.Remoting.Giraffe

open SAFE.Remoting.Shared

let getRecord() =
    async {
        return { Value = 42
                 Greeting = "Hello from Fable.Remoting!" }
    }

let remotingApi = { getRecord = getRecord }

let httpHandler: Giraffe.Core.HttpFunc -> Microsoft.AspNetCore.Http.HttpContext -> Giraffe.Core.HttpFuncResult =
    Remoting.createApi()
    |> Remoting.withRouteBuilder routeBuilder
    |> Remoting.fromValue remotingApi
    |> Remoting.buildHttpHandler
