module Queue 

open System
open Microsoft.WindowsAzure.Storage
open Microsoft.WindowsAzure.Storage.Queue


let private getQueue() =
    let connectionString = Environment.GetEnvironmentVariable("STORAGE_CONNECTION", EnvironmentVariableTarget.Process)
    let storageAccount = CloudStorageAccount.Parse(connectionString)
    let queueClient = storageAccount.CreateCloudQueueClient()
    let queue = queueClient.GetQueueReference("active-awesome-tip-queue")
    async {
        let! _ = queue.CreateIfNotExistsAsync() |> Async.AwaitTask
        return queue
    }

let enqueue msg =
    async {
        let! queue = getQueue()
        return! 
            msg
            |> CloudQueueMessage
            |> queue.AddMessageAsync |> Async.AwaitTask
    }