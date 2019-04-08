module ActiveAwesome.Queue 

open Microsoft.WindowsAzure.Storage
open Microsoft.WindowsAzure.Storage.Queue
open ActiveAwesome.JsonHelper

type Queue =
    { EnqueueGitHubIssue: Tip -> Async<Result<unit, string>> 
      EnqueueGitHubCommit: Tip -> Async<Result<unit, string>> 
      EnqueueSlackResponse: Tip * IssueUrl -> Async<Result<unit, string>> 
      EnqueueSlackNotification: NotEmptyString -> Async<Result<unit, string>> }

let private getQueue azureStorageConnectionString name =
    let storageAccount = CloudStorageAccount.Parse(azureStorageConnectionString)
    let queueClient = storageAccount.CreateCloudQueueClient()
    let queue = queueClient.GetQueueReference(name)
    async {
        let! _ = queue.CreateIfNotExistsAsync() |> Async.AwaitTask
        return queue
    }

let private enqueue azureStorageConnectionString name msg =
    asyncResult {
        try 
            let! queue = getQueue azureStorageConnectionString name
            return! 
                msg
                |> CloudQueueMessage
                |> queue.AddMessageAsync 
                |> Async.AwaitTask
        with
            | exn -> return! exn.ToString() |> Error
    }

let private enqueueGitHubIssue azureStorageConnectionString tip =
    async {
        return!
            tip 
            |> serialize
            |> enqueue azureStorageConnectionString "active-awesome-github-issue"
    }

let private enqueueGitHubCommit azureStorageConnectionString tip =
    async {
        return!
            tip 
            |> serialize
            |> enqueue azureStorageConnectionString "active-awesome-github-commit"
    }

let private enqueueSlackResponse azureStorageConnectionString item =
    async {
        return!
            item
            |> serialize
            |> enqueue azureStorageConnectionString "active-awesome-slack-response"
    }

let private enqueueSlackNotification azureStorageConnectionString item =
    async {
        return!
            item
            |> serialize
            |> enqueue azureStorageConnectionString "active-awesome-slack-notification"
    }

let queue azureStorageConnectionString =
    { EnqueueGitHubIssue = enqueueGitHubIssue azureStorageConnectionString
      EnqueueGitHubCommit = enqueueGitHubCommit azureStorageConnectionString 
      EnqueueSlackResponse = enqueueSlackResponse azureStorageConnectionString
      EnqueueSlackNotification = enqueueSlackNotification azureStorageConnectionString }