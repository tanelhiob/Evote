open System
open System.Net.Http
open Newtonsoft.Json
open System.Text
open System.Text.RegularExpressions
open FSharp.Data

[<CLIMutable>]
type Vote = {
    Voter: string
    VoterToken: string
    Choice: string
    ChoiceToken: string
}

type Login = {
    Name: string
    Secret: string
}

type State = {
    Login: Login option
    Vote: Vote option
}

type response_vote = { Name: string; Token: string }
type response_voter = { Value: string; Token: string}


[<EntryPoint>]
let main _ =
    
    let login state name =
        printfn "You are now %s" name
        let login = {
            Name = name
            Secret = Guid.NewGuid().ToString() }
        { state with Login = Some login }
                   
    let voteAsync state choice =
        match state with
        | { Login = Some login } ->
            async {
                let vote = {
                    Voter = login.Name
                    VoterToken = login.Secret
                    Choice = choice
                    ChoiceToken = login.Secret
                }

                if state.Vote.IsSome then
                    printfn "Changing vote from %s to %s" state.Vote.Value.Choice choice

                let voteJson = JsonConvert.SerializeObject(vote)
                let body = HttpRequestBody.TextRequest voteJson
                let url = "http://localhost:7071/api/CastVote"
                do! Http.AsyncRequestString(url, httpMethod = "POST", body = body) |> Async.Ignore

                return { state with Vote = Some vote }
            }            
        | _ -> async { return state }

    let check state =
        async {
            if state.Vote.IsSome then
                printfn "You voted for %s" state.Vote.Value.Choice

            let url = "http://localhost:7071/api/GetVotes"
            let! json = Http.AsyncRequestString(url)        

            let (voters, votes) = JsonConvert.DeserializeObject<(response_vote list)*(response_voter list)>(json)

            printfn "%A" voters
            printfn "%A" votes
        }
    
    let list state input =
        state

    let results state input = 
        state

    let (|Regex|_|) pattern input =
        let m = Regex.Match(input, pattern)
        if m.Success then Some(List.tail [ for g in m.Groups -> g.Value ])
        else None

    let rec processInput state = 
        printfn "Awaiting your command..."
        let input = Console.ReadLine()

        match input with 
        | Regex "^login ([a-zA-Z]+)$" [username] -> login state username |> processInput
        | Regex "^vote ([a-z0-9]+)$" [choice] -> voteAsync state choice |> Async.RunSynchronously |> processInput
        | "check" -> check state |> Async.RunSynchronously; processInput state
        | "exit" -> ignore()
        | _ -> printfn "invalid input"; processInput state

    processInput { Login = None; Vote = None }

    // use httpClient = new HttpClient()

    //let token = Guid.NewGuid().ToString()
    //let vote = {
    //    Voter = "Tanel"
    //    VoterToken = token
    //    Choice = "fhhfh"
    //    ChoiceToken = token
    //}

    //let json = JsonConvert.SerializeObject(vote)
    //let content = new StringContent(json, Encoding.UTF8, "application/json")
    //let response = httpClient.PostAsync ("http://localhost:7071/api/CastVote", content) |> Async.AwaitTask |> Async.RunSynchronously
    //response.EnsureSuccessStatusCode() |> ignore

    // let response = httpClient.GetStringAsync("http://localhost:7071/api/GetVotes") |> Async.AwaitTask |> Async.RunSynchronously
    // printfn "%s" response

    0