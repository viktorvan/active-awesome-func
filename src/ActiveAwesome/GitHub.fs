module ActiveAwesome.GitHub

open FSharp.Data
open FSharp.Data.HttpRequestHeaders
open FSharpPlus
open Cvdm.ErrorHandling
open ActiveAwesome
open System
open System.Text

type GitHubIssueJson = JsonProvider<"GitHubIssue.json">

type GitHubIssue = GitHubIssueJson.Root

type GitHubCreateIssueResponseJson = JsonProvider<"GitHubCreateIssueResponse.json">

type GitHubCreateIssueResponse = GitHubCreateIssueResponseJson.Root

type PushEventJson = JsonProvider<"PushEvent.json">

type PushEvent = PushEventJson.Root

type Commit = PushEventJson.Commit

type FileContentResponseJson = JsonProvider<"GitHubGetContentResponse.json">

type FileContentResponse = FileContentResponseJson.Root

type UpdateContentRequestJson = JsonProvider<"GitHubUpdateContentResponse.json">

type UpdateContentRequest = UpdateContentRequestJson.Root

type Committer = UpdateContentRequestJson.Committer

type GitHub =
    { CreateIssue : Tip -> Async<Result<IssueUrl, string>>
      ParseWebHookNotification : NotEmptyString -> Result<NotEmptyString option, string>
      AddCommit : Tip -> Async<Result<unit, string>> }

let private createIssue gitHubApiUrl gitHubAuth tip =
    let gitHubIssueApi = sprintf "%s/issues" gitHubApiUrl

    let issueJson =
        let username = tip.Username |> NotEmptyString.value
        let url = tip.Url |> NotEmptyString.value
        let title = sprintf "Please add this tip to the awesome collection on behalf of %s" username

        let fullText =
            sprintf "Add the following tip in a suitable place:\n%s (added by %s)\n" url username
        GitHubIssue(title, fullText).JsonValue.ToString()
    asyncResult {
        try
            let! response = Http.AsyncRequestString
                                (gitHubIssueApi,
                                 headers = [ ContentType HttpContentTypes.Json
                                             Authorization gitHubAuth
                                             UserAgent "active-awesome-slack" ], body = TextRequest issueJson)
            return! parseWith GitHubCreateIssueResponseJson.Parse response
                    |> Result.map (fun r -> r.HtmlUrl)
                    |> Result.bind (NotEmptyString.create "issueUrl")
        with exn -> return! exn.ToString() |> Error
    }

let private parseWebHookNotification json =
    result {
        try
            let! commits = json
                           |> NotEmptyString.value
                           |> parseWith PushEventJson.Parse
                           |> Result.map (fun event -> event.Commits)
            return! commits
                    |> Array.filter (fun (c : Commit) -> c.Message.StartsWith("tip:"))
                    |> Array.tryLast
                    |> Option.map (fun commit ->
                           sprintf "New tip from @%s!\n%s\n%s" commit.Author.Name commit.Message Settings.gitHubRepoUrl)
                    |> Option.map (fun str -> NotEmptyString.create "commit msg" str)
                    |> sequence
        with exn -> return! exn.ToString() |> Error
    }

let private getFileFromRepo gitHubApiUrl gitHubAuth =
    let bodyAsString response =
        match response.Body with
        | Text text -> Ok text
        | Binary _ -> Error "Expected text response"

    let decodeBase64String str =
        result {
            try
                return! str
                        |> Convert.FromBase64String
                        |> Encoding.UTF8.GetString
                        |> NotEmptyString.create "decoded base64 github file content"
            with exn -> return! exn.ToString() |> Error
        }

    asyncResult {
        try
            let! response = Http.AsyncRequest(gitHubApiUrl,
                                              headers = [ Authorization gitHubAuth
                                                          UserAgent "active-awesome-slack" ])
                            |> Async.map HttpResponse.ensureSuccessStatusCode
            let! bodyJson = bodyAsString response
            let! parsed = parseWith FileContentResponseJson.Parse bodyJson
            let! content = parsed.Content |> decodeBase64String
            let! fileSha = parsed.Sha |> NotEmptyString.create "File SHA"
            return (content, fileSha)
        with exn -> return! exn.ToString() |> Error
    }

let private addCommit gitHubApiUrl gitHubAuth tip =
    let gitHubContentUrl = sprintf "%s/contents/%s" gitHubApiUrl "pending.MD"
    let tipUrl = tip.Url |> NotEmptyString.value
    let username = tip.Username |> NotEmptyString.value
    let encodeBase64 str = Encoding.UTF8.GetBytes(s = str) |> Convert.ToBase64String
    asyncResult {
        let! (content, sha) = getFileFromRepo gitHubContentUrl gitHubAuth
        let contentStr = NotEmptyString.value content

        let newContent =
            contentStr + (sprintf "- %s (added by %s)\n" tipUrl username)
            |> encodeBase64

        let committer = Committer(username, "active-awesome-slack@activesolution.se")
        let json = UpdateContentRequest(tipUrl, committer, newContent, sha |> NotEmptyString.value).JsonValue.ToString()
        return! Http.AsyncRequest(gitHubContentUrl, httpMethod = "PUT",
                                  headers = [ ContentType HttpContentTypes.Json
                                              Authorization gitHubAuth
                                              UserAgent "active-awesome-slack" ], body = TextRequest json)
                |> Async.map HttpResponse.ensureSuccessStatusCode
                |> Async.Ignore
    }

let gitHub gitHubApiUrl gitHubAuth =
    { CreateIssue = createIssue gitHubApiUrl gitHubAuth
      ParseWebHookNotification = parseWebHookNotification
      AddCommit = addCommit gitHubApiUrl gitHubAuth }
