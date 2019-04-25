module ActiveAwesome.Slack

open FSharp.Data
open ActiveAwesome.JsonHelper
open FSharp.Data.HttpRequestHeaders
open FsToolkit.ErrorHandling

type Attachment =
    { text : string }

type SlackResponse =
    { text : string
      attachments : Attachment [] }

type Slack =
    { RespondIssueCreated : SlackResponseUrl -> IssueUrl -> Async<Result<unit, string>>
      RespondWithError : SlackResponseUrl -> NotEmptyString -> Async<Result<unit, string>>
      SendSlackNotification : NotEmptyString -> Async<Result<unit, string>>
      ParseWebHookNotification : Tip -> Result<NotEmptyString, string> }

let respondToSlack url json =
    asyncResult {
        try
            return! Http.AsyncRequest
                        (url |> NotEmptyString.value, headers = [ ContentType HttpContentTypes.Json ],
                         body = TextRequest json)
                    |> Async.Ignore
        with exn -> return! exn.ToString() |> Error
    }
let private respondIssueCreated slackResponseUrl issueUrl =
    let response =
        let issueUrlStr = NotEmptyString.value issueUrl
        { text = "An issue has been created for your new tip"
          attachments = [| { text = issueUrlStr } |] }
    let json = response |> serialize
    respondToSlack slackResponseUrl json

let respondWithError responseUrl errorMsg =
    let response =
        { text = errorMsg |> NotEmptyString.value |> sprintf "Something went wrong when processing your tip: %s"
          attachments = [| |] }
    let json = response |> serialize
    respondToSlack responseUrl json

let private sendSlackNotification slackWebHookUrl notification =
    let notification = NotEmptyString.value notification
    asyncResult {
        try
            let notificationJson =
                { text = notification
                  attachments = [||] }
                |> serialize
            return! Http.AsyncRequest
                        (slackWebHookUrl, headers = [ ContentType HttpContentTypes.Json ],
                         body = TextRequest notificationJson)
                    |> Async.Ignore
        with exn -> return! exn.ToString() |> Error
    }

let private parseWebHookNotification gitHubRepoUrl tip =
    let username = tip.Username |> NotEmptyString.value
    let url = tip.Text |> NotEmptyString.value
    sprintf "New tip from @%s!\n%s\n%s" username url gitHubRepoUrl
    |> NotEmptyString.createWithName "slack notification"

let slack slackWebHookUrl gitHubRepoUrl =
    { RespondIssueCreated = respondIssueCreated
      RespondWithError = respondWithError
      SendSlackNotification = sendSlackNotification slackWebHookUrl
      ParseWebHookNotification = parseWebHookNotification gitHubRepoUrl }
