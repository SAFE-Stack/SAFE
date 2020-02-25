namespace SAFE.Remoting

open SAFE
open SAFE.Core

type Remoting () =
    inherit SAFEPlugin()
    interface ISAFESharedPlugin
    interface ISAFEClientPlugin
    interface ISAFEServerPlugin
    override __.Snippets =
        [ "src/Server/Server.fs", 
            [ "router {", """        forward "/api" SAFE.Remoting.Server.httpHandler""" ]
          "src/Client/Client.fs", 
            [ "^type Msg =", "    | RemotingMsg of SAFE.Remoting.Shared.Record"
              "^let init ()", "    let remotingCmd () = SAFE.Remoting.Client.createCmd RemotingMsg" ] ]
