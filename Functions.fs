namespace ActiveAwesomeFunctions

open Microsoft.Azure.WebJobs
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Mvc
open FSharp.Data
open System.Web.Http
open System
open Microsoft.Extensions.Logging
open ActiveAwesomeFunctions.JsonHelper
open ActiveAwesomeFunctions

type PushEventJson = JsonProvider<"PushEvent.json", EmbeddedResource="ActiveAwesomeFunctions, ActiveAwesomeFunctions.PushEvent.json">

type PushEvent = PushEventJson.Root

module Functions =
    [<FunctionName("Status")>]
    let runStatus ([<HttpTrigger(Extensions.Http.AuthorizationLevel.Anonymous, "get")>] req: HttpRequest) =
        ContentResult(Content = "Ok", ContentType = "text/html")

    [<FunctionName("KeepAlive")>]
    let runKeepAlive ([<TimerTrigger("0 */4 * * * *")>] myTimer, log: ILogger) =
        DateTime.Now.ToString() |> sprintf "Executed at %s" |> log.LogInformation

    [<FunctionName("AddTip")>]
    let runAddTip ([<HttpTrigger(Extensions.Http.AuthorizationLevel.Anonymous, "post")>] req: HttpRequest, log: ILogger) =
        asyncResult {
            let! tip = req |> Tip.fromHttpRequest
            let! issueQueued = Queue.enqueueGitHubIssue tip
            let! commitQueued = Queue.enqueueGitHubCommit tip
            match issueQueued, commitQueued with 
            | Ok _, Ok _ -> return ContentResult(Content = "Ok, adding your tip now...", ContentType = "text/plain") :> ActionResult
            | _ -> return InternalServerErrorResult() :> ActionResult
        } 
        |> Async.map (Result.raiseError log "AddTip")
        |> Async.StartAsTask

    [<FunctionName("CreateGitHubIssue")>]
    let runCreateGitHubIssue ([<QueueTrigger("active-awesome-github-issue")>] queueItem: string, log: ILogger) =
        let parseIssueUrl (issue : GitHubCreateIssueResponse) =
            issue.HtmlUrl
        asyncResult {
            let! tip = deserialize queueItem
            let! issue = CreateGitHubIssue.execute Settings.gitHubIssueApi Settings.gitHubAuth tip
            return! Queue.enqueueSlackResponse (tip, issue |> parseIssueUrl)
        } 
        |> Async.map (Result.raiseError log "CreateGitHubIssue")
        |> Async.StartAsTask

    [<FunctionName("CreateGitHubCommit")>]
    let runCreateGitHubCommit ([<QueueTrigger("active-awesome-github-commit")>] queueItem: string, log: ILogger) =
        asyncResult {
            let! tip = deserialize queueItem
            let username = tip.Username |> NotEmptyString.value
            let url = tip.Url |> NotEmptyString.value
            let! _ = CreateGitHubCommit.execute Settings.gitHubRepoUrl Settings.gitHubRepoUrlWithAuth tip 
            let notification = sprintf "New tip from @%s!\n%s\n%s" username url Settings.gitHubRepoUrl
            return! Queue.enqueueSlackNotification notification
        } 
        |> Async.map (Result.raiseError log "CreateGitHubCommit") 
        |> Async.StartAsTask 

    [<FunctionName("RespondToSlack")>] 
    let runRespondToSlack ([<QueueTrigger("active-awesome-slack-response")>] queueItem: string, log: ILogger) =
        asyncResult {
            let! (tip, issueUrl) = deserialize queueItem
            let! _ = RespondToSlack.execute tip issueUrl
            return ()
        } 
        |> Async.map (Result.raiseError log "RespondToSlack") 
        |> Async.StartAsTask 

    [<FunctionName("GitHubWebHook")>]
    let runGitHubWebHook ([<HttpTrigger(Extensions.Http.AuthorizationLevel.Anonymous, "post")>] req: HttpRequest, log: ILogger) =
        let parseNotification (event:PushEvent) =
            event.Commits
            |> Array.filter (fun (c:Commit) -> c.Message.StartsWith("tip:")) 
            |> Array.tryLast 
            |> Option.map (fun commit -> sprintf "New tip from @%s!\n%s\n%s" commit.Author.Name commit.Message Settings.gitHubRepoUrl)

        asyncResult {
            let! pushEvent = PushEvent.fromHttpRequest req
            let notification = parseNotification pushEvent
            let! _ =
                match notification with
                | Some n -> Queue.enqueueSlackNotification n 
                | None -> asyncResult { return () }
            return ContentResult(Content = "Ok", ContentType = "text/html")
        } 
        |> Async.map (Result.raiseError log "GitHubWebHook")
        |> Async.StartAsTask

    [<FunctionName("NotifySlack")>]
    let runNotifySlack ([<QueueTrigger("active-awesome-slack-notification")>] queueItem: string, log: ILogger) =
        NotifySlack.execute queueItem 
        |> Result.raiseError log "NotifySlack"
        |> ignore
