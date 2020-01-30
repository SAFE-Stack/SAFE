namespace SAFE.Remoting

open SAFE
open SAFE.Core

type Remoting () =
    inherit SAFEPlugin()
    interface ISAFESharedPlugin
    interface ISAFEClientPlugin
    interface ISAFEServerPlugin
