[<AutoOpen>]
module ActiveAwesomeFunctions.Types
open System
open Microsoft.AspNetCore.Http
open System.IO
open System.Web
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

type NotEmptyString with
    member this.Value = NotEmptyString.value this

module HttpRequest =
    let bodyAsString (req: HttpRequest) =
        asyncResult {
            try 
                use stream = new StreamReader(stream = req.Body)
                let! bodyString = stream.ReadToEndAsync() |> Async.AwaitTask 
                return! NotEmptyString.create "HttpRequest body string" bodyString
            with
                | exn -> return! exn.ToString() |> Error
        }

module HttpResponse =
    let ensureSuccessStatusCode (response: FSharp.Data.HttpResponse) =
        if response.StatusCode < 200 && response.StatusCode >= 300 then "HttpRequest failed" |> Error
        else Ok ()


type Tip =
    { Url: NotEmptyString
      Username: NotEmptyString  
      SlackResponseUrl: NotEmptyString }

module Tip =
    let fromHttpRequest (req:HttpRequest) =
        asyncResult {
            let! bodyString = req |> HttpRequest.bodyAsString
            let query = bodyString |> NotEmptyString.value |> HttpUtility.ParseQueryString
            let! url =  query.["text"] |> NotEmptyString.create "url"
            let! username = query.["user_name"] |> NotEmptyString.create "username"
            let! responseUrl = query.["response_url"]  |> NotEmptyString.create "response_url"
            return
                { Url = url 
                  Username = username  
                  SlackResponseUrl = responseUrl }
        }

type IssueUrl = NotEmptyString

exception AwesomeFuncException of string
