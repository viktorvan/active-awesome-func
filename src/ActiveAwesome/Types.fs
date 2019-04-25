[<AutoOpen>]
module ActiveAwesome.Types

open System
open Newtonsoft.Json
open Microsoft.AspNetCore.Http
open System.IO
open System.Web
open Microsoft.Extensions.Logging
open System.Threading.Tasks
open System.Text
open FsToolkit.ErrorHandling

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
                return! stream.ReadToEndAsync() |> Async.AwaitTask
            with exn -> return! exn.ToString() |> Error
        }

module String =
    let stripDiacritics (text: string) =
        let getUnicodeCategory (c:char) = Globalization.CharUnicodeInfo.GetUnicodeCategory c
        let isNonSpacingMark cat = cat <> Globalization.UnicodeCategory.NonSpacingMark

        let normalizedString = text.Normalize NormalizationForm.FormD
        let sb = StringBuilder()
        [ for c in normalizedString -> c ]
        |> List.filter (getUnicodeCategory >> isNonSpacingMark)
        |> List.iter (sb.Append >> ignore)
        sb.ToString()

[<JsonObject(MemberSerialization = MemberSerialization.Fields)>]
type NotEmptyString = private NotEmptyString of string

module NotEmptyString =
    let createWithName name str =
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

type SlackResponseUrl = NotEmptyString
type Tip =
    { Text : NotEmptyString
      Username : NotEmptyString
      SlackResponseUrl : SlackResponseUrl option }

module Tip =
    let fromHttpRequest (req : HttpRequest) =
        let validateUri uri = if Uri.IsWellFormedUriString(uri, UriKind.Absolute) then Some uri else None
        asyncResult {
            let! bodyString = req |> HttpRequest.bodyAsString
            let query = bodyString |> HttpUtility.ParseQueryString
            let! text = query.["text"] |> String.stripDiacritics |> NotEmptyString.createWithName "tip text"
            let! username = query.["user_name"] |> NotEmptyString.createWithName "username"
            let! responseUrl = query.["response_url"] |> validateUri |> Option.map (NotEmptyString.createWithName "response_url") |> Option.sequenceResult
            
            return { Text = text
                     Username = username
                     SlackResponseUrl = responseUrl }
        }

let parseWith parser str =
    try
        parser str |> Ok
    with exn -> exn.ToString() |> Error
