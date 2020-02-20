module SAFE.Remoting.Shared

/// Defines how routes are generated on server and mapped from client
let routeBuilder typeName methodName =
    sprintf "/api/%s/%s" typeName methodName

type Record =
    { Value : int
      Greeting : string }

/// A type that specifies the communication protocol between client and server
/// to learn more, read the docs at https://zaid-ajaj.github.io/Fable.Remoting/src/basics.html
type IRemotingApi =
    { getRecord : unit -> Async<Record> }