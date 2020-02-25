namespace SAFE.Azure

open System
open System.Net

open Fake.Core
open Fake.IO
open Fake.IO.Globbing.Operators
open Cit.Helpers.Arm
open Cit.Helpers.Arm.Parameters
open Microsoft.Azure.Management.ResourceManager.Fluent.Core

open SAFE

type ArmOutput =
    { WebAppName : ParameterValue<string>
      WebAppPassword : ParameterValue<string> }

// https://github.com/SAFE-Stack/SAFE-template/issues/120
// https://stackoverflow.com/a/6994391/3232646
type TimeoutWebClient() =
    inherit WebClient()
    override this.GetWebRequest uri =
        let request = base.GetWebRequest uri
        request.Timeout <- 30 * 60 * 1000
        request

type AppService() =
    inherit SAFEPlugin()

    let mutable deploymentOutputs : ArmOutput option = None

    let deployDir = Path.getFullName "./deploy"

    let armTemplate () =
        let environment = Environment.environVarOrDefault "environment" (Guid.NewGuid().ToString().ToLower().Split '-' |> Array.head)
        let armTemplate = @"arm-template.json"
        let resourceGroupName = "safe-" + environment

        let authCtx =
            // You can safely replace these with your own subscription and client IDs hard-coded into this script.
            let subscriptionId = try Environment.environVar "subscriptionId" |> Guid.Parse with _ -> failwith "Invalid Subscription ID. This should be your Azure Subscription ID."
            let clientId = try Environment.environVar "clientId" |> Guid.Parse with _ -> failwith "Invalid Client ID. This should be the Client ID of a Native application registered in Azure with permission to create resources in your subscription."

            Trace.tracefn "Deploying template '%s' to resource group '%s' in subscription '%O'..." armTemplate resourceGroupName subscriptionId
            subscriptionId
            |> authenticateDevice Trace.trace { ClientId = clientId; TenantId = None }
            |> Async.RunSynchronously

        let deployment =
            let location = Environment.environVarOrDefault "location" Region.EuropeWest.Name
            let pricingTier = Environment.environVarOrDefault "pricingTier" "F1"
            { DeploymentName = "SAFE-template-deploy"
              ResourceGroup = New(resourceGroupName, Region.Create location)
              ArmTemplate = IO.File.ReadAllText armTemplate
              Parameters =
                  Simple
                      [ "environment", ArmString environment
                        "location", ArmString location
                        "pricingTier", ArmString pricingTier ]
              DeploymentMode = Incremental }

        deployment
        |> deployWithProgress authCtx
        |> Seq.iter(function
            | DeploymentInProgress (state, operations) -> Trace.tracefn "State is %s, completed %d operations." state operations
            | DeploymentError (statusCode, message) -> Trace.traceError <| sprintf "DEPLOYMENT ERROR: %s - '%s'" statusCode message
            | DeploymentCompleted d -> deploymentOutputs <- d)

    let appService () =
        let zipFile = "deploy.zip" 
        IO.File.Delete zipFile
        Zip.zip deployDir zipFile !!(deployDir + @"\**\**")

        let appName = deploymentOutputs.Value.WebAppName.value
        let appPassword = deploymentOutputs.Value.WebAppPassword.value

        let destinationUri = sprintf "https://%s.scm.azurewebsites.net/api/zipdeploy" appName
        let client = new TimeoutWebClient(Credentials = NetworkCredential("$" + appName, appPassword))
        Trace.tracefn "Uploading %s to %s" zipFile destinationUri
        client.UploadData(destinationUri, IO.File.ReadAllBytes zipFile) |> ignore

    interface ISAFEDeployablePlugin with
        member this.Deploy () =
            armTemplate ()
            appService ()

    interface ISAFEServerPlugin

    override __.Snippets =
        [ "src/Server/Server.fs", 
            [ "application {", "        service_config SAFE.Azure.AppService.Server.configureAzure" ] ]