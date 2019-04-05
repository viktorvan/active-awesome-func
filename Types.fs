[<AutoOpen>]
module ActiveAwesomeFunctions.Types
open System
open Microsoft.AspNetCore.Http
open System.IO
open System.Web
open FSharp.Data
open Newtonsoft.Json

[<JsonObject(MemberSerialization = MemberSerialization.Fields)>]
type NotEmptyString = 
    private | NotEmptyString of string

module NotEmptyString =
    let create name str =
        if String.IsNullOrWhiteSpace str then
            name |> sprintf "%s cannot be empty" |> Error
        else
            str |> NotEmptyString |> Ok

    let value (NotEmptyString str) = str


module Internal =
    let bodyAsString (req: HttpRequest) =
        asyncResult {
            try 
                use stream = new StreamReader(stream = req.Body)
                return! stream.ReadToEndAsync() |> Async.AwaitTask 
            with
                | exn -> return! exn.ToString() |> Error
        }
open Internal
open Newtonsoft.Json
open Microsoft.Extensions.Logging

type Tip =
    { Url: NotEmptyString
      Username: NotEmptyString  
      SlackResponseUrl: NotEmptyString }

module Tip =
    let fromHttpRequest (req:HttpRequest) =
        asyncResult {
            let! bodyString = req |> bodyAsString
            let query = HttpUtility.ParseQueryString bodyString
            let! url =  query.["text"] |> NotEmptyString.create "url"
            let! username = query.["user_name"] |> NotEmptyString.create "username"
            let! responseUrl = query.["response_url"]  |> NotEmptyString.create "response_url"
            return
                { Url = url 
                  Username = username  
                  SlackResponseUrl = responseUrl }
        }

type GitHubIssue = JsonProvider<Samples.IssueSample, RootName="issue">
type GitHubCreateIssueResponseJson = JsonProvider<"GitHubCreateIssueResponse.json", EmbeddedResource="ActiveAwesomeFunctions, ActiveAwesomeFunctions.GitHubCreateIssueResponse.json">
type GitHubCreateIssueResponse = GitHubCreateIssueResponseJson.Root
type Attachment = { text: string }
type SlackResponse = { text: string; attachments: Attachment [] }

type IssueUrl = NotEmptyString


type PushEventJson = JsonProvider<"PushEvent.json", EmbeddedResource="ActiveAwesomeFunctions, ActiveAwesomeFunctions.PushEvent.json">
type PushEvent = PushEventJson.Root
type Commit = PushEventJson.Commit

module PushEvent =
    let fromHttpRequest (req:HttpRequest) : Async<Result<PushEvent, string>> =
        asyncResult {
            let! json = bodyAsString req
            return PushEventJson.Parse json
        }

exception AwesomeFuncException of string

module Result =
    let raiseError (log: ILogger) functionName = function
    | Ok r -> r
    | Error e -> 
        (functionName, e) ||> sprintf "%s failed with error %s" |> log.LogError
        e |> AwesomeFuncException |> raise