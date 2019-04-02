namespace ActiveAwesomeFunctions

open Microsoft.AspNetCore.Http

open System.IO
open System.Web
open Newtonsoft.Json
open Microsoft.WindowsAzure.Storage
open ActiveAwesomeFunc
open ActiveAwesomeFunc.HttpRequestHelper

module AddTip =
    let parseSlackData (req: HttpRequest) =
        async {
            let! bodyString = req |> bodyAsString
            let query = HttpUtility.ParseQueryString bodyString
            return 
                { Url = query.["text"]
                  Username = query.["user_name"]
                  ResponseUrl = query.["response_url"] }
        }

    let run (req: HttpRequest) =
        parseSlackData req
        |> Async.bind (JsonConvert.SerializeObject >> Queue.enqueue)