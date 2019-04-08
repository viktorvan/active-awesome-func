module ActiveAwesomeFunctions.GitHub

open FSharp.Data
open FSharp.Data.HttpRequestHeaders
open FSharpPlus
open Cvdm.ErrorHandling
open System.IO
open ActiveAwesomeFunctions
open System
open System.Text
open Microsoft.AspNetCore.Http


type GitHubIssue = JsonProvider<Samples.IssueSample, RootName="issue">
type GitHubCreateIssueResponseJson = JsonProvider<"GitHubCreateIssueResponse.json", EmbeddedResource="ActiveAwesomeFunctions, ActiveAwesomeFunctions.GitHubCreateIssueResponse.json">
type GitHubCreateIssueResponse = GitHubCreateIssueResponseJson.Root

type PushEventJson = JsonProvider<"PushEvent.json", EmbeddedResource="ActiveAwesomeFunctions, ActiveAwesomeFunctions.PushEvent.json">
type PushEvent = PushEventJson.Root
type Commit = PushEventJson.Commit

type FileContentResponseJson = JsonProvider<Samples.ContentResponse>
type FileContentResponse = FileContentResponseJson.Root
type UpdateContentRequestJson = JsonProvider<Samples.UpdateContentRequest>
type UpdateContentRequest = UpdateContentRequestJson.Root
type Committer = UpdateContentRequestJson.Committer

type GitHub =
    { CreateIssue: Tip -> Async<Result<IssueUrl, string>> 
      ParseWebHookNotification: NotEmptyString -> Result<NotEmptyString option, string> 
      AddCommit: Tip -> Async<Result<unit, string>> }

let private buildIssueJson command = 
    let username = command.Username |> NotEmptyString.value
    let url = command.Url |> NotEmptyString.value
    let title = sprintf "Please add this tip to the awesome collection on behalf of %s" username
    let fullText = sprintf "Add the following tip in a suitable place:\n%s (added by %s)\n" url username 
    GitHubIssue.Issue(title, fullText).JsonValue.ToString()

let private createIssue gitHubApiUrl gitHubAuth tip =
    let gitHubIssueApi = sprintf "%s/issues" gitHubApiUrl
    asyncResult {
        try
            let json =
                tip
                |> buildIssueJson
            let! jsonResponse =
                Http.AsyncRequestString
                    ( gitHubIssueApi, 
                      headers = 
                          [ ContentType HttpContentTypes.Json 
                            Authorization gitHubAuth
                            UserAgent "active-awesome-slack" ],
                      body = TextRequest json) 
            let! response = parseWith GitHubCreateIssueResponseJson.Parse jsonResponse
            return! response.HtmlUrl |> NotEmptyString.create "issueUrl"
        with
            | exn -> return! exn.ToString() |> Error
    }

let private parseWebHookNotification json =
    result {
        try
            let! parsed = 
                json
                |> NotEmptyString.value
                |> parseWith PushEventJson.Parse 
            return!
                parsed
                |> fun event -> event.Commits
                |> Array.filter (fun (c:Commit) -> c.Message.StartsWith("tip:")) 
                |> Array.tryLast 
                |> Option.map (fun commit -> sprintf "New tip from @%s!\n%s\n%s" commit.Author.Name commit.Message Settings.gitHubRepoUrl)
                |> Option.map (fun str -> NotEmptyString.create "commit msg" str)
                |> sequence
        with
            | exn -> return! exn.ToString() |> Error
    }

let private appendTipToFile path tipUrl tipUsername =
    asyncResult {
        try
            let! lines = File.ReadAllLinesAsync(path) |> Async.AwaitTask |> Async.map Seq.ofArray
            let newLine = (sprintf "%s (added by %s)" tipUrl tipUsername) |> Seq.singleton
            let updated = lines |> Seq.append newLine |> Array.ofSeq
            return File.WriteAllLines(path, updated)
        with
            | exn -> return! exn.ToString() |> Error
    }

let private getFileFromRepo gitHubApiUrl gitHubAuth =

    let bodyAsString response = 
        match response.Body with
        | Text text -> Ok text
        | Binary _ -> Error "Expected text response"

    let decodeBase64String file =
        result {
            try
                return!
                    file
                    |> Convert.FromBase64String
                    |> Encoding.UTF8.GetString
                    |> NotEmptyString.create "decoded base64 github file content"
            with
                | exn -> return! exn.ToString() |> Error
        }

    asyncResult {
        try
            let! response = 
                Http.AsyncRequest(
                    gitHubApiUrl,
                    headers = 
                        [ Authorization gitHubAuth
                          UserAgent "active-awesome-slack" ])
                |> Async.map HttpResponse.ensureSuccessStatusCode
            let! bodyJson = bodyAsString response
            let! parsed = parseWith FileContentResponseJson.Parse bodyJson
            let! content = parsed.Content |> decodeBase64String
            let! fileSha = parsed.Sha |> NotEmptyString.create "File SHA"
            return (content, fileSha) 
        with
            | exn -> return! exn.ToString() |> Error
    }

let private addCommit gitHubApiUrl gitHubAuth tip = 
    let gitHubContentUrl = sprintf "%s/contents/%s" gitHubApiUrl "pending.Md"
    let url = tip.Url |> NotEmptyString.value
    let username = tip.Username |> NotEmptyString.value

    asyncResult {
        let! (content, sha) = getFileFromRepo gitHubContentUrl gitHubAuth 
        let newContent =
            content
            |> NotEmptyString.value
            |> (+) (sprintf "\n%s (added by %s)\n\n" url username)
        let committer = Committer(username, "")
        let json = UpdateContentRequest(url, committer, newContent, sha |> NotEmptyString.value).JsonValue.ToString()
        return!
            Http.AsyncRequest
                ( gitHubContentUrl, 
                  httpMethod = "PUT", 
                  headers = 
                      [ ContentType HttpContentTypes.Json 
                        Authorization gitHubAuth
                        UserAgent "active-awesome-slack" ],
                  body = TextRequest json) 
            |> Async.map HttpResponse.ensureSuccessStatusCode
            |> Async.Ignore
    }

let gitHub gitHubApiUrl gitHubAuth =
    { CreateIssue = createIssue gitHubApiUrl gitHubAuth 
      ParseWebHookNotification = parseWebHookNotification 
      AddCommit = addCommit gitHubApiUrl gitHubAuth }
