module SAFE.Azure.AppService.Server

open Microsoft.Extensions.DependencyInjection
open Microsoft.WindowsAzure.Storage

open SAFE.Server

let storageAccount =
    Environment.getEnvVar
        "STORAGE_CONNECTIONSTRING"
        "UseDevelopmentStorage=true"
    |> CloudStorageAccount.Parse

let configureAzure (services: IServiceCollection) =
    Environment.tryGetEnvVar "APPINSIGHTS_INSTRUMENTATIONKEY"
    |> Option.map services.AddApplicationInsightsTelemetry
    |> Option.defaultValue services
