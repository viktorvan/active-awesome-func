module ActiveAwesomeFunctions.Settings

open System.Text
open System

let requireNotNull key str =
    if String.IsNullOrWhiteSpace str then invalidArg key (sprintf "missing environment variable: %s" key)
    else str

let private getEnvironmentVariable key =
    Environment.GetEnvironmentVariable(key, EnvironmentVariableTarget.Process) 
    |> requireNotNull key


let private gitHubRepo = getEnvironmentVariable "GITHUB_REPO"

let gitHubRepoUrl = 
        gitHubRepo 
        |> sprintf " https://github.com/%s"

let private gitHubUsernameRaw =
    getEnvironmentVariable "GITHUB_USERNAME"

let gitHubUsername = 
    getEnvironmentVariable "GITHUB_USERNAME"

let private gitHubPasswordRaw =
    getEnvironmentVariable "GITHUB_PASSWORD"
    
let gitHubPassword = 
    getEnvironmentVariable "GITHUB_PASSWORD"

let gitHubRepoUrlWithAuth = 
    gitHubRepo 
    |> sprintf" https://%s:%s@github.com/%s" gitHubUsernameRaw gitHubPasswordRaw

let gitHubApiUrl = 
    gitHubRepo 
    |> sprintf "https://api.github.com/repos/%s"

let gitHubAuth =
    let base64 = 
        sprintf "%s:%s" gitHubUsernameRaw gitHubPasswordRaw 
        |> Encoding.ASCII.GetBytes 
        |> Convert.ToBase64String
    sprintf "Basic %s" base64

let slackWebhookUrl = getEnvironmentVariable "SLACK_WEBHOOK_URL"
let azureStorageConnectionString = getEnvironmentVariable "STORAGE_CONNECTION"