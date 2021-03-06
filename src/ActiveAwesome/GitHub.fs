module ActiveAwesome.GitHub

open FSharp.Data
open FSharp.Data.HttpRequestHeaders
open ActiveAwesome
open System
open System.Text
open FsToolkit.ErrorHandling

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
      ParseWebHookNotification : string -> Result<NotEmptyString option, string>
      AddCommit : Tip -> Async<Result<unit, string>> }

let private createIssue gitHubApiUrl gitHubAuth tip =
    let gitHubIssueApi = sprintf "%s/issues" gitHubApiUrl

    let issueJson =
        let username = tip.Username |> NotEmptyString.value
        let tipText = tip.Text |> NotEmptyString.value
        let title = sprintf "%s has added a new tip: %s" username tipText

        let fullText = "Please move this tip from pending.MD to a more suitable place.\n"
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
                    |> Result.bind (NotEmptyString.createWithName "issueUrl")
        with exn -> return! exn.ToString() |> Error
    }

let private parseWebHookNotification json =
    result {
        try
            let! commits = json
                           |> parseWith PushEventJson.Parse
                           |> Result.map (fun event -> event.Commits)
            return! commits
                    |> Array.filter (fun (c : Commit) -> c.Message.StartsWith("tip:"))
                    |> Array.tryLast
                    |> Option.map (fun commit ->
                           sprintf "New tip from @%s!\n%s\n%s" commit.Author.Name commit.Message Settings.gitHubRepoUrl)
                    |> Option.map (fun str -> NotEmptyString.createWithName "commit msg" str)
                    |> Option.sequenceResult
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
                return str
                        |> Convert.FromBase64String
                        |> Encoding.UTF8.GetString
            with exn -> return! exn.ToString() |> Error
        }

    asyncResult {
        try
            let! response = Http.AsyncRequest(gitHubApiUrl,
                                              headers = [ Authorization gitHubAuth
                                                          UserAgent "active-awesome-slack" ])
                                 
            let! bodyJson = bodyAsString response
            let! parsed = parseWith FileContentResponseJson.Parse bodyJson
            let! content = parsed.Content |> decodeBase64String
            let! fileSha = parsed.Sha |> NotEmptyString.createWithName "File SHA"
            return (content, fileSha)
        with exn -> return! exn.ToString() |> Error
    }

let private addCommit gitHubApiUrl gitHubAuth tip =
    let gitHubContentUrl = sprintf "%s/contents/%s" gitHubApiUrl "pending.MD"
    let tipText = tip.Text |> NotEmptyString.value
    let username = tip.Username |> NotEmptyString.value
    let encodeBase64 str = Encoding.UTF8.GetBytes(s = str) |> Convert.ToBase64String
    asyncResult {
        try 
            let! (contentStr, sha) = getFileFromRepo gitHubContentUrl gitHubAuth

            let newContent =
                contentStr + (sprintf "- %s (added by %s)\n" tipText username)
                |> encodeBase64

            let committer = Committer(username, "active-awesome-slack@activesolution.se")
            let json = UpdateContentRequest(tipText, committer, newContent, sha |> NotEmptyString.value).JsonValue.ToString()
            return! Http.AsyncRequest(gitHubContentUrl, httpMethod = "PUT",
                                      headers = [ ContentType HttpContentTypes.Json
                                                  Authorization gitHubAuth
                                                  UserAgent "active-awesome-slack" ], 
                                                  body = TextRequest json)
                    |> Async.Ignore
        with exn -> return! exn.ToString() |> Error
    }

let gitHub gitHubApiUrl gitHubAuth =
    { CreateIssue = createIssue gitHubApiUrl gitHubAuth
      ParseWebHookNotification = parseWebHookNotification
      AddCommit = addCommit gitHubApiUrl gitHubAuth }
