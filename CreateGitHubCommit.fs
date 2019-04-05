module ActiveAwesomeFunctions.CreateGitHubCommit

open Fake.Tools
open System.IO
open Fake.IO
open ActiveAwesomeFunctions


[<Literal>]
let TempFolder = "tmp"

[<Literal>]
let Filename = "pending.MD"

let private cleanFolder name =
    Directory.delete name
    Directory.create name

let private cloneRepository repoUrl =
    Git.Repository.clone TempFolder repoUrl "."

let private appendTipToFile tipUrl tipUsername =
    let path = sprintf "%s/%s" TempFolder Filename
    async {
        let! lines = File.ReadAllLinesAsync(path) |> Async.AwaitTask |> Async.map Seq.ofArray
        let newLine = (sprintf "%s (added by %s)" tipUrl tipUsername) |> Seq.singleton
        let updated = lines |> Seq.append newLine |> Array.ofSeq
        File.WriteAllLines(path, updated)
    }

let private pushCommit gitHubRepoWithAuth tipUrl tipUsername =
    Git.Staging.stageFile TempFolder Filename |> ignore
    Git.Commit.exec TempFolder (sprintf "%s (added by %s\n" tipUrl tipUsername)
    let command = sprintf "push %s --all" gitHubRepoWithAuth
    if Git.CommandHelper.directRunGitCommand TempFolder command then ()
    else failwith "Git push failed"

let private commitPendingTip gitHubRepoUrl gitHubRepoWithAuth tip =
    let tipUrl = tip.Url |> NotEmptyString.value
    let tipUsername = tip.Username |> NotEmptyString.value

    asyncResult {
        try
            cleanFolder TempFolder
            cloneRepository gitHubRepoUrl
            let! _ = appendTipToFile tipUrl tipUsername
            return pushCommit gitHubRepoWithAuth tipUrl tipUsername
        with
            | exn -> return! exn.ToString() |> Error 
    }

let execute gitHubRepoUrl gitHubRepoWithAuth (tip: Tip) = 
    commitPendingTip gitHubRepoUrl gitHubRepoWithAuth tip

