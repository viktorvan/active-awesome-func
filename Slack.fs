module ActiveAwesomeFunctions.Slack

open FSharp.Data
open Microsoft.AspNetCore.Http
open System.IO
open ActiveAwesomeFunctions.JsonHelper
open FSharp.Data.HttpRequestHeaders


type Attachment = { text: string }
type SlackResponse = { text: string; attachments: Attachment [] }


type Slack =
    { RespondToSlack: Tip -> NotEmptyString -> Async<Result<unit, string>> 
      SendSlackNotification: NotEmptyString -> Async<Result<unit, string>> 
      ParseWebHookNotification: Tip -> Result<NotEmptyString, string> }


let private buildResponse url =
    { text = "An issue has been created for your new tip" 
      attachments = [| { text = url } |] }

let respondToSlack tip issueUrl =
    let issueUrl = NotEmptyString.value issueUrl
    let slackResponseUrl = tip.SlackResponseUrl |> NotEmptyString.value
    let slackResponseJson =
        buildResponse issueUrl
        |> serialize

    asyncResult {
         
        try 
            return!
                Http.AsyncRequest
                  (slackResponseUrl, 
                   headers = [ ContentType HttpContentTypes.Json ],
                   body = TextRequest slackResponseJson)
                |> Async.map HttpResponse.ensureSuccessStatusCode
        with
            | exn -> return! exn.ToString() |> Error
    }

let private sendSlackNotification slackWebHookUrl notification =
    let notification = NotEmptyString.value notification
    asyncResult {
        try 
            let notificationJson = 
                { text = notification; attachments = [||] }
                |> serialize
            return!
                Http.AsyncRequest
                  (slackWebHookUrl, 
                   headers = [ ContentType HttpContentTypes.Json ],
                   body = TextRequest notificationJson)
                |> Async.map HttpResponse.ensureSuccessStatusCode
        with
            | exn -> return! exn.ToString() |> Error
    }

let private parseWebHookNotification gitHubRepoUrl tip =
    let username = tip.Username |> NotEmptyString.value
    let url = tip.Url |> NotEmptyString.value
    sprintf "New tip from @%s!\n%s\n%s" username url gitHubRepoUrl 
    |> NotEmptyString.create "slack notification"

let slack slackWebHookUrl gitHubRepoUrl =
    { RespondToSlack = respondToSlack 
      SendSlackNotification = sendSlackNotification slackWebHookUrl 
      ParseWebHookNotification = parseWebHookNotification gitHubRepoUrl }