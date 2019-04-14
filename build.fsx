open Fake.Core
#r "paket:
nuget FSharp.Core 4.5.4
nuget Fake.Core.Target
nuget Fake.JavaScript.Npm
nuget Fake.DotNet.Cli
nuget Fake.Dotnet.Testing.Expecto
nuget Fake.IO.FileSystem //"
#load "./.fake/build.fsx/intellisense.fsx"

#if !FAKE
#r "netstandard"
#r "Facades/netstandard" // https://github.com/ionide/ionide-vscode-fsharp/issues/839#issuecomment-396296095

#endif

#nowarn "52"

open Fake.Core.TargetOperators
open Fake.DotNet
open Fake.IO
open Fake.IO.Globbing.Operators
open Fake.JavaScript


let paketFile = if Environment.isWindows then "paket.exe" else "paket"
let paketExe = System.IO.Path.Combine(__SOURCE_DIRECTORY__, ".paket", paketFile)
let funcFile = if Environment.isWindows then "func.cmd" else "func"

let deployDir = Environment.environVarOrDefault "DEPLOY_DIR" (Path.getFullName "./deploy")
let functionsPath = Path.getFullName "./src/ActiveAwesome"
let configuration =
    match Environment.environVarOrDefault "BEEKEEP_CONFIGURATION" "release" with
    | "debug" -> DotNet.BuildConfiguration.Debug
    | _ -> DotNet.BuildConfiguration.Release
let resourceGroupName = Environment.environVarOrDefault "RESOURCEGROUP_NAME" "active-awesome"
let location = Environment.environVarOrDefault "LOCATION" "westeurope"
let storageName = Environment.environVarOrDefault "STORAGE_NAME" "activeawesomestorage"
let sku = Environment.environVarOrDefault "SKU" "Standard_LRS"
let functionAppName = Environment.environVarOrDefault "FUNCTIONAPP_NAME" "active-awesome-func"
let gitHubRepo = Environment.environVar "GITHUB_REPO" 
let gitHubUsername = Environment.environVar "GITHUB_USERNAME" 
let gitHubPassword = Environment.environVar "GITHUB_PASSWORD" 
let slackWebhookUrl = Environment.environVar "SLACK_WEBHOOK_URL" 
let storageConnection = Environment.environVar "STORAGE_CONNECTION"
let queues = [ "active-awesome-github-issue"; "active-awesome-github-commit"; "active-awesome-slack-response"; "active-awesome-slack-notification" ]

let runTool cmd args workingDir =
    let arguments =
        args
        |> String.split ' '
        |> Arguments.OfArgs
    Command.RawCommand(cmd, arguments)
    |> CreateProcess.fromCommand
    |> CreateProcess.withWorkingDirectory workingDir
    |> CreateProcess.ensureExitCode
    |> Proc.run
    |> ignore

let azCli args = 
    let arguments =
        args
        |> String.split ' '
        |> Arguments.OfArgs
    Command.RawCommand("az", arguments)
    |> CreateProcess.fromCommand
    |> CreateProcess.withWorkingDirectory "."
    // |> CreateProcess.ensureExitCode
    |> Proc.run
    |> ignore
let funcCli = runTool funcFile

Target.create "Clean" (fun _ ->
        Shell.cleanDirs [ deployDir ]
        !! "src/**/bin/**/*"
        ++ "src/**/obj/**/*"
        |> File.deleteAll
    )

Target.create "Build" (fun _ -> DotNet.build (fun p -> { p with Configuration = configuration }) functionsPath)

Target.create "SetupAzureResources" (fun _ ->
    let createQueue name = azCli (sprintf "storage queue create --name %s --account-name %s" name storageName)
    azCli (sprintf "group create --name %s --location %s" resourceGroupName location)
    azCli (sprintf "storage account create --name %s --location %s --resource-group %s --sku %s" storageName location resourceGroupName sku)
    queues |> List.iter createQueue
    azCli (sprintf "functionapp create --resource-group %s --consumption-plan-location %s --name %s --storage-account %s --runtime dotnet" resourceGroupName location functionAppName storageName)
)

Target.create "Publish" (fun _ ->
    DotNet.publish (fun p -> 
        { p with Configuration = DotNet.BuildConfiguration.Release
                 OutputPath = Some deployDir}) 
        functionsPath
    let host = sprintf "%s/host.json" functionsPath
    Shell.copyFile deployDir host
)

Target.create "InstallTools" (fun _ ->
    if Environment.isWindows then
        Npm.exec "install -g azure-functions-core-tools" id

    if not (File.exists paketExe) then
        DotNet.exec id "tool" "install --tool-path \".paket\" Paket --add-source https://api.nuget.org/v3/index.json"
        |> ignore
    else
        printfn "paket already installed"
    funcCli "--version" "."
    azCli "--version"
)

Target.create "Deploy" (fun _ ->
    funcCli (sprintf "azure functionapp publish %s" functionAppName) deployDir
    azCli (sprintf "functionapp config appsettings set GITHUB_REPO=%s GITHUB_USERNAME=%s GITHUB_PASSWORD=%s SLACK_WEBHOOK_URL=%s STORAGE_CONNECTION=%s" gitHubRepo gitHubUsername gitHubPassword slackWebhookUrl storageConnection)
)

Target.create "DeployWithLocalSettings" (fun _ ->
    let settings = sprintf "%s/local.settings.json" functionsPath
    Shell.copyFile deployDir settings
    funcCli (sprintf "azure functionapp publish %s --publish-local-settings" functionAppName) deployDir
)

"Clean" 
    ==> "InstallTools"
    ==> "Publish"
    ==> "DeployWithLocalSettings"

"InstallTools"
    ==> "Build"

"InstallTools"
    ==> "Deploy"

Target.runOrDefaultWithArguments "Build"