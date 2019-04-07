namespace ActiveAwesomeFunctions

open Microsoft.Extensions.Logging
open System.Threading.Tasks


module Result =
    let raiseError (log: ILogger) functionName = function
    | Ok r -> r
    | Error e -> 
        (functionName, e) ||> sprintf "%s failed with error %s" |> log.LogError
        e |> AwesomeFuncException |> raise

module Async =
    let bind fA xA = 
        async {
            let! x = xA
            return! fA x
        }

    let map f xA =
        async {
            let! x = xA
            return f x
        }

    let runAsTaskT log name ``async`` =
        ``async`` 
        |> map (Result.raiseError log name)
        |> Async.StartAsTask
    let runAsTask log name ``async``  =
        runAsTaskT log name ``async``  :> Task