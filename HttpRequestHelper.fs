module ActiveAwesomeFunc.HttpRequestHelper
open System.IO
open Microsoft.AspNetCore.Http

let bodyAsString (req: HttpRequest) =
    use stream = new StreamReader(stream = req.Body)
    stream.ReadToEndAsync() |> Async.AwaitTask 