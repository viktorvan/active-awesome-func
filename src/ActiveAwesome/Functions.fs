module ActiveAwesome.Functions

open Microsoft.Azure.WebJobs
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Mvc
open System.Web.Http
open System
open Microsoft.Extensions.Logging
open ActiveAwesome.JsonHelper
open ActiveAwesome
open FsToolkit.ErrorHandling

let gitHub = GitHub.gitHub Settings.gitHubApiUrl Settings.gitHubAuth
let slack = Slack.slack Settings.slackWebhookUrl Settings.gitHubRepoUrl
let queue = Queue.queue Settings.azureStorageConnectionString

[<FunctionName("Status")>]
let runStatus ([<HttpTrigger(Extensions.Http.AuthorizationLevel.Anonymous, "get")>] req : HttpRequest) =
    ContentResult(Content = "Ok", ContentType = "text/html")

[<FunctionName("KeepAlive")>]
let runKeepAlive ([<TimerTrigger("0 */4 * * * *")>] myTimer, log : ILogger) =
    DateTime.Now.ToString()
    |> sprintf "Executed at %s"
    |> log.LogInformation

[<FunctionName("AddTip")>]
let runAddTip ([<HttpTrigger(Extensions.Http.AuthorizationLevel.Anonymous, "post")>] req : HttpRequest, log : ILogger) =
    asyncResult {
        let! tip = req |> Tip.fromHttpRequest
        let! issueQueued = queue.EnqueueGitHubIssue tip
        let! commitQueued = queue.EnqueueGitHubCommit tip
        match issueQueued, commitQueued with
        | Ok _, Ok _ ->
            return ContentResult(Content = "Ok, adding your tip now...", ContentType = "text/plain") :> ActionResult
        | _ -> return InternalServerErrorResult() :> ActionResult
    }
    |> Async.runAsTaskT log "AddTip"

[<FunctionName("CreateGitHubIssue")>]
let runCreateGitHubIssue ([<QueueTrigger("active-awesome-github-issue")>] queueItem : string, log : ILogger) =
    asyncResult { let! tip = deserialize queueItem
                  let! issueUrl = gitHub.CreateIssue tip
                  return! queue.EnqueueSlackResponse(tip, issueUrl) } |> Async.runAsTask log "CreateGitHubIssue"

[<FunctionName("HandleCreateGitHubIssueError")>]
let handleCommitError ([<QueueTrigger("active-awesome-github-issue-poison")>] queueItem : string, log : ILogger) =
    asyncResult { let! tip = deserialize queueItem
                  sprintf "Handling error from CreateGitHubIssue for queueItem %s" queueItem |> log.LogInformation
                  let! errorMsg = NotEmptyString.createWithName "errorMsg" "Failed to create github issue"
                  match tip.SlackResponseUrl with
                  | Some url ->
                      return! slack.RespondWithError url errorMsg 
                  | None -> return () } |> Async.runAsTask log "HandleCreateGitHubIssueError"

[<FunctionName("CreateGitHubCommit")>]
let runCreateGitHubCommit ([<QueueTrigger("active-awesome-github-commit")>] queueItem : string, log : ILogger) =
    asyncResult { let! tip = deserialize queueItem
                  let! _ = gitHub.AddCommit tip
                  let! notification = slack.ParseWebHookNotification tip
                  return! queue.EnqueueSlackNotification notification } |> Async.runAsTask log "CreateGitHubCommit"

[<FunctionName("HandleCreateGitHubCommitError")>]
let handleIssueError ([<QueueTrigger("active-awesome-github-commit-poison")>] queueItem : string, log : ILogger) =
    asyncResult { let! tip = deserialize queueItem
                  sprintf "Handling error from CreateGitHubCommit for queueItem %s" queueItem |> log.LogInformation
                  let! errorMsg = NotEmptyString.createWithName "errorMsg" "Failed to create github commit"
                  match tip.SlackResponseUrl with
                  | Some url -> 
                      return! slack.RespondWithError url errorMsg 
                  | None -> return () } |> Async.runAsTask log "HandleCreateGitHubCommitError"

[<FunctionName("RespondToSlack")>]
let runRespondToSlack ([<QueueTrigger("active-awesome-slack-response")>] queueItem : string, log : ILogger) =
    asyncResult { let! (tip, issueUrl) = deserialize queueItem
                  match tip.SlackResponseUrl with
                  | Some url ->
                      return! slack.RespondIssueCreated url issueUrl 
                  | None -> return () } |> Async.runAsTask log "RespondToSlack"

[<FunctionName("GitHubWebHook")>]
let runGitHubWebHook ([<HttpTrigger(Extensions.Http.AuthorizationLevel.Anonymous, "post")>] req : HttpRequest,
                      log : ILogger) =
    asyncResult {
        let! json = HttpRequest.bodyAsString req
        let! notification = gitHub.ParseWebHookNotification json
        let! _ = match notification with
                 | Some n -> n |> queue.EnqueueSlackNotification
                 | None -> asyncResult { return () }
        return ContentResult(Content = "Ok", ContentType = "text/html")
    }
    |> Async.runAsTaskT log "GitHubWebHook"

[<FunctionName("NotifySlack")>]
let runNotifySlack ([<QueueTrigger("active-awesome-slack-notification")>] queueItem : string, log : ILogger) =
    asyncResult { let! notification = queueItem |> deserialize
                  return! slack.SendSlackNotification notification } |> Async.runAsTask log "NotifySlack"
