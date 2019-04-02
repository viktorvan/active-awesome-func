namespace ActiveAwesomeFunctions

open Microsoft.Azure.WebJobs
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Mvc
open FSharp.Data
open System.Web.Http
open System
open Microsoft.Extensions.Logging

type PushEventJson = JsonProvider<"PushEvent.json", EmbeddedResource="ActiveAwesomeFunc, ActiveAwesomeFunc.PushEvent.json">

type PushEvent = PushEventJson.Root

module Functions =
    [<FunctionName("Status")>]
    let runAlive ([<HttpTrigger(Extensions.Http.AuthorizationLevel.Anonymous, "get")>] req : HttpRequest) =
        ContentResult(Content = "Ok", ContentType = "text/html")

    [<FunctionName("KeepAlive")>]
    let runKeepAlive ([<TimerTrigger("0 */4 * * * *")>] myTimer, log: ILogger) =
        DateTime.Now.ToString() |> sprintf "Executed at %s" |> log.LogInformation

    [<FunctionName("PostToSlack")>]
    let runPostToSlack ([<HttpTrigger(Extensions.Http.AuthorizationLevel.Anonymous, "post")>] req : HttpRequest) =
        async {
            match! PostToSlack.run req with
            | Some result -> return ContentResult(Content = result, ContentType = "text/html")
            | None -> return ContentResult(Content = "No tip found", ContentType = "text/html")
        } |> Async.StartAsTask

    [<FunctionName("AddTip")>]
    let runAddTip ([<HttpTrigger(Extensions.Http.AuthorizationLevel.Anonymous, "post")>] req : HttpRequest) =
        async {
            let! _ = AddTip.run req 
            return ContentResult(Content = "Ok", ContentType = "text/html ")
        } |> Async.StartAsTask

    [<FunctionName("CreateGitHubIssue")>]
    let runCreateGitHubIssue ([<QueueTrigger("active-awesome-tip-queue")>] queueItem: string) =
        CreateGitHubIssue.run queueItem 
        |> Async.Ignore
        |> Async.StartAsTask
