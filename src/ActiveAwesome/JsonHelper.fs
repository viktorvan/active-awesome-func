module ActiveAwesome.JsonHelper

open Newtonsoft.Json

let serialize = JsonConvert.SerializeObject

let deserialize str : Result<'T, string> =
    try
        JsonConvert.DeserializeObject<'T> str |> Ok
    with
        | exn -> exn.ToString() |> Error