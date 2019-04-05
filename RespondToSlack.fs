module ActiveAwesomeFunctions.RespondToSlack

open ActiveAwesomeFunctions.JsonHelper
open FSharp.Data
open FSharp.Data.HttpRequestHeaders

let private buildResponse url =
    { text = "An issue has been created for your new tip" 
      attachments = [| { text = url } |] }

let execute tip issueUrl =
    let slackResponseUrl = tip.SlackResponseUrl |> NotEmptyString.value
    let slackResponseJson =
        buildResponse issueUrl 
        |> serialize

    Http.AsyncRequest
      (slackResponseUrl, 
       headers = [ ContentType HttpContentTypes.Json ],
       body = TextRequest slackResponseJson)

