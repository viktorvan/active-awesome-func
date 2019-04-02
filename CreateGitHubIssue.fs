module ActiveAwesomeFunctions.CreateGitHubIssue

open FSharp.Data
open FSharp.Data.HttpRequestHeaders
open Fake.Tools

open System.IO
open System.Web
open System.Text
open System
open Newtonsoft.Json
open ActiveAwesomeFunc

[<Literal>]
let private IssueSample = """
{
  "title": "Found a bug",
  "body": "I'm having a problem with this.",
  "assignees": [
    "octocat"
  ],
  "milestone": 1,
  "labels": [
    "bug"
  ]
}
"""

type GitHubIssue = JsonProvider<IssueSample, RootName="issue">
type GitHubCreateIssueResponseJson = JsonProvider<"GitHubCreateIssueResponse.json", EmbeddedResource="ActiveAwesomeFunc, ActiveAwesomeFunc.GitHubCreateIssueResponse.json">
type GitHubCreateIssueResponse = GitHubCreateIssueResponseJson.Root
type Attachment = { text: string }
type SlackResponse = { response_type: string; text: string; attachments: Attachment [] }

let private buildIssueJson username url = 
    let title = sprintf "Please add this tip to the awesome collection on behalf of %s" username
    let fullText = 
        sprintf """Add the following tip in a suitable place:
        %s

        and use the following commit message:
        
        tip:%s added a tip to active-awesome: %s
        """ url username url
    GitHubIssue.Issue(title, fullText, [||], 0, [| |]).JsonValue.ToString()
    
let private auth =
    let username = Environment.GetEnvironmentVariable("GITHUB_USERNAME", EnvironmentVariableTarget.Process)
    let password = Environment.GetEnvironmentVariable("GITHUB_PASSWORD", EnvironmentVariableTarget.Process)
    let base64 = sprintf "%s:%s" username password |> Encoding.ASCII.GetBytes |> Convert.ToBase64String
    sprintf "Basic %s" base64

let private commitPendingTip payload =
    async {
        Git.Repository.clone "tmp" "https://github.com/viktorvan/active-awesome" "."
        let path = "tmp/pending.MD"
        let! lines = File.ReadAllLinesAsync(path) |> Async.AwaitTask |> Async.map Seq.ofArray
        let newLine = (sprintf "%s added new tip: %s" payload.Username payload.Url) |> Seq.singleton
        let updated = lines |> Seq.append newLine |> Array.ofSeq
        File.WriteAllLines(path, updated)
        Git.Staging.stageFile "tmp" "readme.MD" |> ignore
        Git.Commit.exec "tmp" (sprintf "tip:%s;%s" payload.Url payload.Username)
        Git.Branches.push "tmp"
    }

let createNewIssue payload = 
    let newIssue = buildIssueJson payload.Username payload.Url
    Http.AsyncRequestString
      ( "https://api.github.com/repos/viktorvan/active-awesome/issues", 
        headers = [ ContentType HttpContentTypes.Json; Authorization auth; UserAgent "active-awesome-slack" ],
        body = TextRequest newIssue)

let private buildResponse url =
    { response_type = "in_channel"
      text = "Ok, I've created a new issue for that:"
      attachments = [| { text = url } |] }

let run (item: string) =
        let payload = JsonConvert.DeserializeObject<SlackPayload> item

        async {

            let! _ = commitPendingTip payload

            let! response = createNewIssue payload

            return
                response
                |> GitHubCreateIssueResponseJson.Parse
                |> fun (r:GitHubCreateIssueResponse) -> r.HtmlUrl
                |> buildResponse 
                |> Ok
        }

