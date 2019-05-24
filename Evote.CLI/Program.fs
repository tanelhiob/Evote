open System
open System.Text.RegularExpressions
open Handlers

let wrapAsync state = async { return state }

let wrapAsyncWithState state (expression: Async<unit>) = async {
    do! expression
    return state
}

let (|Guid|_|) (input: string) =
    match Guid.TryParse(input) with
    | (true, guid) -> Some guid
    | (false, _) -> None

let (|Regex|_|) pattern input =
    let matches = Regex.Match(input, pattern)
    if matches.Success then Some(List.tail [ for g in matches.Groups -> g.Value ])
    else None

let (|Split|) (input: string) =
    input.Split(" ") |> Array.toList 

[<EntryPoint>]
let main _ =

    let rec handleInputAsync state = async {
        printfn "Awaiting %s command..." (state.Login |> Option.bind (fun login -> Some (login.Name + "'s")) |> Option.defaultValue "your")
        printfn "login; start; list; select; vote; check; exit;"

        let input = Console.ReadLine()
        
        if input = "exit" then
           printfn "exiting..."
        else 
            let! state =
                match input with 
                | Regex "^login ([a-zA-Z]+)$" [username] -> login state username |> wrapAsync
                | Split (command::campaignName::choices) when command = "start" -> startCampaignAsync state campaignName choices
                | "list" -> listCampaignsAsync |> wrapAsyncWithState state
                | Regex "^select ([a-z0-9\\-]+)$" [campaignId] ->
                    match campaignId with
                    | Guid id -> selectCampaignAsync state id
                    | _ -> printfn "Invalid guid"; wrapAsync state 
                | Regex "^vote ([a-z0-9]+)$" [choice] -> voteAsync state choice
                | "check" -> check state |> wrapAsyncWithState state
                | _ -> printfn "invalid input"; wrapAsync state
            printfn ""

            do! handleInputAsync state 
    }

    handleInputAsync { Login = None; Vote = None; Campaign = None } |> Async.RunSynchronously

    0