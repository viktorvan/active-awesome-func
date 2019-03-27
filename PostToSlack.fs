namespace ActiveAwesomeFunctions

open Microsoft.AspNetCore.Http
open FSharp.Data
open Fake.Api
open System.IO
open System

module PostToSlack =
    type PushEventJson = JsonProvider<"PushEvent.json", EmbeddedResource="ActiveAwesomeFunc, ActiveAwesomeFunc.PushEvent.json">
    type PushEvent = PushEventJson.Root

    let parsePushEvent (req:HttpRequest) =
        use stream = new StreamReader(req.Body)
        let json = stream.ReadToEndAsync() |> Async.AwaitTask |> Async.RunSynchronously
        PushEventJson.Parse(json)

    let parseTipText (pushEvent : PushEvent) =
        let repositoryUrl = pushEvent.Repository.FullName |> sprintf "https://github.com/%s"
        let author = pushEvent.Pusher.Name
        let commitMsg = 
            pushEvent.Commits 
            |> Array.map (fun c -> c.Message) 
            |> Array.filter (fun msg -> msg.StartsWith("tip:")) 
            |> Array.tryLast 

        let buildTipText author (msg:string option) repositoryUrl = 
            match msg with
            | None -> None
            | Some msg -> sprintf "New tip from %s!\n%s\n%s" author (msg.[ 4.. ]) repositoryUrl |> Some

        buildTipText author commitMsg repositoryUrl

    let slackNotificationBuilder text =
        fun (p: Slack.NotificationParams) ->
            { p with
                Text = text
                Channel = "#active-awesome-test"
                IconEmoji = ":exclamation:" } 


    let run (req: HttpRequest) =
        let webhookUrl = Environment.GetEnvironmentVariable("WEBHOOK_URL", EnvironmentVariableTarget.Process)

        let tipText =
            req
            |> parsePushEvent
            |> parseTipText

        match tipText with
        | None -> None
        | Some text ->
            Slack.sendNotification webhookUrl (slackNotificationBuilder text) 
            |> Some
