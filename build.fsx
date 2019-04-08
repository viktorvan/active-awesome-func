open Fake.Core
#r "paket:
nuget FSharp.Core 4.5.4
nuget Fake.Core.Target
nuget Fake.DotNet.Cli
nuget Fake.Dotnet.Testing.Expecto
nuget Fake.IO.FileSystem //"
#load "./.fake/build.fsx/intellisense.fsx"

#if !FAKE
#r "netstandard"
#r "Facades/netstandard" // https://github.com/ionide/ionide-vscode-fsharp/issues/839#issuecomment-396296095

#endif

#nowarn "52"

open System
open Fake.Core
open Fake.Core.TargetOperators
open Fake.DotNet
open Fake.IO
open Fake.IO.Globbing.Operators

let deployDir = Path.getFullName "./deploy"
let functionsPath = Path.getFullName "./src/ActiveAwesome"
let configuration =
    match Environment.environVarOrDefault "BEEKEEP_CONFIGURATION" "release" with
    | "debug" -> DotNet.BuildConfiguration.Debug
    | _ -> DotNet.BuildConfiguration.Release
let resourceGroupName = Environment.environVarOrDefault "RESOURCEGROUP_NAME" "active-awesome"
let location = Environment.environVarOrDefault "LOCATION" "westeurope"
let storageName = Environment.environVarOrDefault "STORAGE_NAME" "activeawesomestorage"
let sku = Environment.environVarOrDefault "SKU" "Standard_LRS"
let queues = [ "active-awesome-github-issue"; "active-awesome-github-commit"; "active-awesome-slack-response"; "active-awesome-slack-notification" ]
let functionAppName = Environment.environVarOrDefault "FUNCTIONAPP_NAME" "active-awesome-func"

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

let azCli = runTool "az"
let funcCli = runTool "func"

Target.create "Clean" (fun _ ->
        Shell.cleanDirs [ deployDir ]
        !! "src/**/bin/**/*"
        ++ "src/**/obj/**/*"
        |> File.deleteAll
    )

Target.create "Build" (fun _ -> DotNet.build (fun p -> { p with Configuration = configuration }) functionsPath)

Target.create "SetupInfrastructure" (fun _ ->
    let createQueue name = azCli (sprintf "storage queue create --name %s --account-name %s" name resourceGroupName) "."
    azCli (sprintf "group create --name %s --location %s" resourceGroupName location) "."
    azCli (sprintf "storage account create --name %s --location %s --resource-group %s --sku %s" storageName location resourceGroupName sku) "."
    queues |> List.iter createQueue
    azCli (sprintf "functionapp create --resource-group %s --consumption-plan-location %s --name %s --storage-account %s --runtime dotnet" resourceGroupName location functionAppName storageName) "."
)

Target.create "Publish" (fun _ ->
    DotNet.publish (fun p -> 
        { p with Configuration = DotNet.BuildConfiguration.Release
                 OutputPath = Some deployDir}) 
        functionsPath
)

Target.create "Deploy" (fun _ ->
    let settings = sprintf "%s/local.settings.json" functionsPath
    let host = sprintf "%s/host.json" functionsPath
    Shell.copyFile deployDir settings
    Shell.copyFile deployDir host
    funcCli (sprintf "azure functionapp publish %s --publish-local-settings" functionAppName) deployDir
)

"Clean" 
    ==> "Publish"
    ==> "Deploy"

Target.runOrDefaultWithArguments "Build"