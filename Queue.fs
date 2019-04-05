module ActiveAwesomeFunctions.Queue 

open Microsoft.WindowsAzure.Storage
open Microsoft.WindowsAzure.Storage.Queue
open ActiveAwesomeFunctions.JsonHelper


let private getQueue name =
    let storageAccount = CloudStorageAccount.Parse(Settings.azureStorageConnectionString)
    let queueClient = storageAccount.CreateCloudQueueClient()
    let queue = queueClient.GetQueueReference(name)
    async {
        let! _ = queue.CreateIfNotExistsAsync() |> Async.AwaitTask
        return queue
    }

let enqueue name msg =
    asyncResult {
        try 
            let! queue = getQueue name
            return! 
                msg
                |> CloudQueueMessage
                |> queue.AddMessageAsync 
                |> Async.AwaitTask
        with
            | exn -> return! exn.ToString() |> Error
    }

let enqueueGitHubIssue tip =
    async {
        return!
            tip 
            |> serialize
            |> enqueue "active-awesome-github-issue"
    }

let enqueueGitHubCommit tip =
    async {
        return!
            tip 
            |> serialize
            |> enqueue "active-awesome-github-commit"
    }

let enqueueSlackResponse item =
    async {
        return!
            item
            |> serialize
            |> enqueue "active-awesome-slack-response"
    }

let enqueueSlackNotification item =
    async {
        return!
            item
            |> enqueue "active-awesome-slack-notification"
    }