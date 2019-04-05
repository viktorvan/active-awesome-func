namespace ActiveAwesomeFunctions

module Async =
    let bind fA xA = 
        async {
            let! x = xA
            return! fA x
        }

    let map f xA =
        async {
            let! x = xA
            return f x
        }