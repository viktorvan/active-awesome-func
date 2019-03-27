namespace ActiveAwesomeFunctions

open Microsoft.Azure.WebJobs
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Mvc
open FSharp.Data
open System.Web.Http

type PushEventJson = JsonProvider<"PushEvent.json", EmbeddedResource="ActiveAwesomeFunc, ActiveAwesomeFunc.PushEvent.json">
type PushEvent = PushEventJson.Root

module Functions =

    [<FunctionName("PostToSlack")>]
    let runPostToSlack
        ([<HttpTrigger(Extensions.Http.AuthorizationLevel.Anonymous, "post")>]
        req: HttpRequest) =
            match PostToSlack.run req with
            | Some result -> ContentResult(Content = result, ContentType = "text/html")
            | None -> ContentResult(Content = "No tip found", ContentType = "text/html")

    [<FunctionName("Alive")>]
    let runAlive
        ([<HttpTrigger(Extensions.Http.AuthorizationLevel.Anonymous, "get")>]
        req: HttpRequest) =
            ContentResult(Content = "Ok", ContentType = "text/html")

    [<FunctionName("CreateGitHubIssue")>]
    let runCreateGitHubIssue
        ([<HttpTrigger(Extensions.Http.AuthorizationLevel.Anonymous, "post")>]
        req: HttpRequest) =
            match CreateGitHubIssue.run req with
            | Ok result -> JsonResult(result) :> IActionResult
            | Error _ -> InternalServerErrorResult() :> IActionResult