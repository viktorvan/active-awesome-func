namespace ActiveAwesomeFunctions

open Microsoft.Azure.WebJobs
open Microsoft.AspNetCore.Http
open FSharp.Data
open FSharp.Data.HttpRequestHeaders

open System.IO
open System.Web
open System.Text
open System

module CreateGitHubIssue =

    type SlackPayload =
        { Url: string
          Username: string
          ResponseUrl: string }

    [<Literal>]
    let issueSample = """
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

    type GitHubIssue = JsonProvider<issueSample, RootName="issue">
    type GitHubCreateIssueResponseJson = JsonProvider<"GitHubCreateIssueResponse.json", EmbeddedResource="ActiveAwesomeFunc, ActiveAwesomeFunc.GitHubCreateIssueResponse.json">
    type GitHubCreateIssueResponse = GitHubCreateIssueResponseJson.Root
    type Attachment = { text: string }
    type SlackResponse = { response_type: string; text: string; attachments: Attachment [] }

    let buildIssueJson username url = 
        let title = sprintf "Please add this tip to the awesome collection on behalf of %s" username
        let fullText = 
            sprintf """Add the following tip in a suitable place:
            %s

            and use the following commit message:
            
            tip:%s added a tip to active-awesome: %s
            """ url username url
        GitHubIssue.Issue(title, fullText, [||], 0, [| |]).JsonValue.ToString()
        

    let parseSlackData (req: HttpRequest) =
        use stream = new StreamReader(req.Body)
        let str = stream.ReadToEndAsync() |> Async.AwaitTask |> Async.RunSynchronously
        let query = HttpUtility.ParseQueryString str
        { Url = query.["text"]
          Username = query.["user_name"]
          ResponseUrl = query.["response_url"] }


    let auth username password =
        let base64 = sprintf "%s:%s" username password |> Encoding.ASCII.GetBytes |> Convert.ToBase64String
        sprintf "Basic %s" base64
    let username = Environment.GetEnvironmentVariable("GITHUB_USERNAME", EnvironmentVariableTarget.Process)
    let password = Environment.GetEnvironmentVariable("GITHUB_PASSWORD", EnvironmentVariableTarget.Process)


    let buildResponse url =
        { response_type = "in_channel"
          text = "Ok, I've created a new issue for that:"
          attachments = [| { text = url } |] }

    let run
        ([<HttpTrigger(Extensions.Http.AuthorizationLevel.Anonymous, "post")>]
        req: HttpRequest) =
            let payload = parseSlackData req
            let newIssue = buildIssueJson payload.Username payload.Url

            let response = Http.RequestString
                              ( "https://api.github.com/repos/viktorvan/active-awesome/issues", 
                                headers = [ ContentType HttpContentTypes.Json; Authorization (auth username password); UserAgent "active-awesome-slack" ],
                                body = TextRequest newIssue)

            response
            |> GitHubCreateIssueResponseJson.Parse
            |> fun (r:GitHubCreateIssueResponse) -> r.HtmlUrl
            |> buildResponse 
            |> Ok

    