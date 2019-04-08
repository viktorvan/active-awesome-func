module ActiveAwesomeFunctions.GitHub

open FSharp.Data
open FSharp.Data.HttpRequestHeaders
open FSharpPlus
open System.IO


type GitHubIssue = JsonProvider<Samples.IssueSample, RootName="issue">
type GitHubCreateIssueResponseJson = JsonProvider<"GitHubCreateIssueResponse.json", EmbeddedResource="ActiveAwesomeFunctions, ActiveAwesomeFunctions.GitHubCreateIssueResponse.json">
type GitHubCreateIssueResponse = GitHubCreateIssueResponseJson.Root

type PushEventJson = JsonProvider<"PushEvent.json", EmbeddedResource="ActiveAwesomeFunctions, ActiveAwesomeFunctions.PushEvent.json">
type PushEvent = PushEventJson.Root
type Commit = PushEventJson.Commit

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

let private parseIssueUrl (issue : GitHubCreateIssueResponse) =
    issue.HtmlUrl

let private createIssue gitHubIssueApi gitHubAuth tip =
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
            let response = GitHubCreateIssueResponseJson.Parse jsonResponse
            return! response.HtmlUrl |> NotEmptyString.create "issueUrl"
        with
            | exn -> return! exn.ToString() |> Error
    }

let private parseWebHookNotification json =
    try
        json 
        |> NotEmptyString.value
        |> PushEventJson.Parse 
        |> fun event -> event.Commits
        |> Array.filter (fun (c:Commit) -> c.Message.StartsWith("tip:")) 
        |> Array.tryLast 
        |> Option.map (fun commit -> sprintf "New tip from @%s!\n%s\n%s" commit.Author.Name commit.Message Settings.gitHubRepoUrl)
        |> Option.map (fun str -> NotEmptyString.create "commit msg" str)
        |> sequence
    with
        | exn -> exn.ToString() |> Error

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

let private addCommit tip = failwith "not implemented"

let gitHub gitHubIssueApi gitHubAuth =
    { CreateIssue = createIssue gitHubIssueApi gitHubAuth 
      ParseWebHookNotification = parseWebHookNotification 
      AddCommit = addCommit }
