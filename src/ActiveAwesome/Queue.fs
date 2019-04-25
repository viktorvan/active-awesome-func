module ActiveAwesome.Queue

open Microsoft.WindowsAzure.Storage
open Microsoft.WindowsAzure.Storage.Queue
open ActiveAwesome.JsonHelper
open FsToolkit.ErrorHandling

type Queue =
    { EnqueueGitHubIssue : Tip -> Async<Result<unit, string>>
      EnqueueGitHubCommit : Tip -> Async<Result<unit, string>>
      EnqueueSlackResponse : Tip * IssueUrl -> Async<Result<unit, string>>
      EnqueueSlackNotification : NotEmptyString -> Async<Result<unit, string>> }

let private getQueue azureStorageConnectionString name =
    let storageAccount = CloudStorageAccount.Parse(azureStorageConnectionString)
    let queueClient = storageAccount.CreateCloudQueueClient()
    let queue = queueClient.GetQueueReference(name)
    async { let! _ = queue.CreateIfNotExistsAsync() |> Async.AwaitTask
            return queue }

let private enqueue azureStorageConnectionString name msg =
    asyncResult {
        try
            let! queue = getQueue azureStorageConnectionString name
            return! msg
                    |> serialize
                    |> CloudQueueMessage
                    |> queue.AddMessageAsync
                    |> Async.AwaitTask
        with exn -> return! exn.ToString() |> Error
    }

let queue azureStorageConnectionString =
    { EnqueueGitHubIssue = enqueue azureStorageConnectionString "active-awesome-github-issue" 
      EnqueueGitHubCommit = enqueue azureStorageConnectionString "active-awesome-github-commit"
      EnqueueSlackResponse = enqueue azureStorageConnectionString "active-awesome-slack-response"
      EnqueueSlackNotification = enqueue azureStorageConnectionString "active-awesome-slack-notification" }
