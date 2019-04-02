namespace ActiveAwesomeFunctions

open Microsoft.AspNetCore.Http
open FSharp.Data
open Fake.Api
open ActiveAwesomeFunc.HttpRequestHelper
open System

module PostToSlack =
    type PushEventJson = JsonProvider<"PushEvent.json", EmbeddedResource="ActiveAwesomeFunc, ActiveAwesomeFunc.PushEvent.json">
    type PushEvent = PushEventJson.Root

    let parsePushEvent (req:HttpRequest) : Async<PushEvent> =
        async {
            let! json = req |> bodyAsString
            return PushEventJson.Parse(json)
        }

    let parseTipText (pushEvent : PushEvent) =
        let repositoryUrl = pushEvent.Repository.FullName |> sprintf "https://github.com/%s"
        let commitMsg = 
            pushEvent.Commits 
            |> Array.map (fun c -> c.Message) 
            |> Array.filter (fun msg -> msg.StartsWith("tip:")) 
            |> Array.tryLast 


        let buildTipText (msg:string option) repositoryUrl = 
            match msg with
            | None -> None
            | Some msg -> 
                let split = msg.Split(';')
                if Array.length split <> 2 then None
                else 
                    let tip, author = (split.[0].[ 4.. ]), split.[1]
                    sprintf "New tip from @%s!\n%s\n%s" author tip repositoryUrl |> Some

        buildTipText commitMsg repositoryUrl

    let slackNotificationBuilder text =
        fun (p: Slack.NotificationParams) ->
            { p with
                Text = text
                Channel = "#active-awesome-test"
                IconEmoji = ":exclamation:" } 

    let run (req: HttpRequest) =
        let webhookUrl = Environment.GetEnvironmentVariable("WEBHOOK_URL", EnvironmentVariableTarget.Process)

        async {
            let! event =
                req
                |> parsePushEvent

            return
                match parseTipText event with
                | None -> None
                | Some text ->
                    Slack.sendNotification webhookUrl (slackNotificationBuilder text) 
                    |> Some
        }
