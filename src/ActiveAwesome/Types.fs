[<AutoOpen>]
module ActiveAwesome.Types

open System
open Newtonsoft.Json
open Microsoft.AspNetCore.Http
open System.IO
open System.Web
open Microsoft.Extensions.Logging
open System.Threading.Tasks

[<JsonObject(MemberSerialization = MemberSerialization.Fields)>]
type NotEmptyString = private NotEmptyString of string

module NotEmptyString =
    let create name str =
        if String.IsNullOrWhiteSpace str then
            name
            |> sprintf "%s cannot be empty"
            |> Error
        else
            str
            |> NotEmptyString
            |> Ok

    let value (NotEmptyString str) = str

type NotEmptyString with
    member this.Value = NotEmptyString.value this

type IssueUrl = NotEmptyString

exception AwesomeFuncException of string

module Result =
    let raiseError (log : ILogger) context result =
        let logException context e =
            (context, e)
            ||> sprintf "%s failed with error %s"
            |> log.LogError

        let raiseException = AwesomeFuncException >> raise
        match result with
        | Ok r -> r
        | Error e ->
            logException context e
            raiseException e

module Async =
    let bind fA xA =
        async { let! x = xA
                return! fA x }
    let map f xA =
        async { let! x = xA
                return f x }

    let runAsTaskT log name async =
        async
        |> map (Result.raiseError log name)
        |> Async.StartAsTask

    let runAsTask log name async = runAsTaskT log name async :> Task

module HttpRequest =
    let bodyAsString (req : HttpRequest) =
        asyncResult {
            try
                use stream = new StreamReader(stream = req.Body)
                return! stream.ReadToEndAsync()
                        |> Async.AwaitTask
                        |> Async.map (NotEmptyString.create "HttpRequest body string")
            with exn -> return! exn.ToString() |> Error
        }

module HttpResponse =
    let ensureSuccessStatusCode (response : FSharp.Data.HttpResponse) =
        let isError = response.StatusCode < 200 && response.StatusCode >= 300
        if isError then "HttpRequest failed" |> Error
        else Ok response

type Tip =
    { Url : NotEmptyString
      Username : NotEmptyString
      SlackResponseUrl : NotEmptyString }

module Tip =
    let fromHttpRequest (req : HttpRequest) =
        asyncResult {
            let! bodyString = req |> HttpRequest.bodyAsString
            let query =
                bodyString
                |> NotEmptyString.value
                |> HttpUtility.ParseQueryString
            let! url = query.["text"] |> NotEmptyString.create "url"
            let! username = query.["user_name"] |> NotEmptyString.create "username"
            let! responseUrl = query.["response_url"] |> NotEmptyString.create "response_url"
            return { Url = url
                     Username = username
                     SlackResponseUrl = responseUrl }
        }

let parseWith parser str =
    try
        parser str |> Ok
    with exn -> exn.ToString() |> Error
