module ActiveAwesomeFunctions.NotifySlack 

open Fake.Api

let slackNotificationBuilder text =
    fun (p: Slack.NotificationParams) ->
        { p with
            Text = text
            Channel = "#active-awesome-test"
            IconEmoji = ":exclamation:" } 

let execute (notification: string) =
    try 
        Slack.sendNotification Settings.slackWebhookUrl (slackNotificationBuilder notification) |> Ok
    with
        | exn -> exn.ToString() |> Error
