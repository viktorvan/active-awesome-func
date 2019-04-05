module ActiveAwesomeFunctions.Settings

open System.Text
open System


let private requireNotNull key value =
    if String.IsNullOrWhiteSpace value then 
        let errorMsg = key |> sprintf "missing environment variable: %s"
        invalidArg key errorMsg
    value

let private getEnvironmentVariable key =
    Environment.GetEnvironmentVariable(key, EnvironmentVariableTarget.Process) 
    |> requireNotNull key

let private gitHubRepo = getEnvironmentVariable "GITHUB_REPO"
let gitHubRepoUrl = sprintf" https://github.com/%s" gitHubRepo
let gitHubUsername = getEnvironmentVariable "GITHUB_USERNAME"
let gitHubPassword = getEnvironmentVariable "GITHUB_PASSWORD"
let gitHubRepoUrlWithAuth = sprintf" https://%s:%s@github.com/%s" gitHubUsername gitHubPassword gitHubRepo
let gitHubIssueApi = sprintf "https://api.github.com/repos/%s/issues" gitHubRepo
let gitHubAuth =
    let base64 = sprintf "%s:%s" gitHubUsername gitHubPassword |> Encoding.ASCII.GetBytes |> Convert.ToBase64String
    sprintf "Basic %s" base64


let slackWebhookUrl = getEnvironmentVariable "SLACK_WEBHOOK_URL"

let azureStorageConnectionString = getEnvironmentVariable "STORAGE_CONNECTION"
