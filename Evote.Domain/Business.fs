namespace Evote.Domain

open System.Security.Cryptography
open System

module Business =
   
    let rsa = new SHA512Managed()

    let generateVoteBindingHash (check:Guid) (secret:Guid) =
        [secret;check]
        |> List.map (fun guid -> guid.ToByteArray())
        |> Array.concat
        |> rsa.ComputeHash
    
    