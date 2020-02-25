namespace SAFE.Server

open System

module Environment =
    let tryGetEnvVar (name: string) =
        Environment.GetEnvironmentVariable name
        |> function
        | null
        | "" -> None
        | x -> Some x

    let getEnvVar (name: string) (defaultValue: string) =
        Environment.GetEnvironmentVariable name
        |> function
        | null
        | "" -> defaultValue
        | x -> x
