module ActiveAwesomeFunctions.CreateGitHubIssue

open FSharp.Data
open FSharp.Data.HttpRequestHeaders
open System.Web


let private buildIssueJson command = 
    let username = command.Username |> NotEmptyString.value
    let url = command.Url |> NotEmptyString.value
    let title = sprintf "Please add this tip to the awesome collection on behalf of %s" username
    let fullText = sprintf "Add the following tip in a suitable place:\n%s (added by %s)\n" url username 
    GitHubIssue.Issue(title, fullText).JsonValue.ToString()

let private postToGitHub gitHubIssueApi gitHubAuth issue : Async<GitHubCreateIssueResponse> =
    async {
        let! jsonResponse =
            Http.AsyncRequestString
              ( gitHubIssueApi, 
                headers = 
                    [ ContentType HttpContentTypes.Json 
                      Authorization gitHubAuth
                      UserAgent "active-awesome-slack" ],
                body = TextRequest issue) 
        return GitHubCreateIssueResponseJson.Parse jsonResponse
    }

let execute gitHubIssueApi gitHubAuth (tip: Tip) =
    asyncResult {
        try
            return!
                tip
                |> buildIssueJson
                |> postToGitHub gitHubIssueApi gitHubAuth
        with
            | exn -> return! exn.ToString() |> Error
    }

