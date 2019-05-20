module Handlers

open System
open Newtonsoft.Json
open FSharp.Data

[<CLIMutable>]
type Vote = {
    CampaignId: Guid
    Voter: string
    VoterToken: string
    Choice: string
    ChoiceToken: string
}

type Login = {
    Name: string
    Secret: string
}

[<CLIMutable>]
type Campaign = {
    Id: Guid
    Name: string
}

type State = {
    Login: Login option
    Vote: Vote option
    Campaign: Campaign option
}

[<CLIMutable>]
type Votes_Choice = {
    Value: string
    Token: string
}

[<CLIMutable>]
type Votes_Voter = {
    Name: string
    Token: string
}

[<CLIMutable>]
type Votes = {
    Choices: Votes_Choice list
    Voters: Votes_Voter list
}

let login state name =
    printfn "You are now %s" name
    let login = {
        Name = name
        Secret = Guid.NewGuid().ToString() }
    { state with Login = Some login }
               
let voteAsync state choice =
    match state with
    | { Login = Some login; Campaign = Some campaign } ->
        async {
            let vote = {
                CampaignId = campaign.Id
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
    | _ -> async {
            printfn "Login and campaign have to be selected"
            return state
        }

let check state = async {
        match state with 
        | { Campaign = Some campaign } -> 
            if state.Vote.IsSome then
                printfn "You voted for %s" state.Vote.Value.Choice

            let url = sprintf "http://localhost:7071/api/GetVotes/%A" campaign.Id
            let! json = Http.AsyncRequestString(url)

            let votes = JsonConvert.DeserializeObject<Votes>(json)

            printfn "%A" votes.Voters
            printfn "%A" votes.Choices

        | _ -> printfn "Campaign has to be selected"
    }

let selectCampaignAsync state campaignId = async {
    let url = sprintf "http://localhost:7071/api/GetCampaign/%A" campaignId
    let! json = Http.AsyncRequestString (url, httpMethod = HttpMethod.Get)
    let campaign = JsonConvert.DeserializeObject<Campaign>(json)
    
    printfn "%s is the active campaign" campaign.Name

    return { state with Campaign = Some campaign }
}

let listCampaignsAsync = async {
    let url = "http://localhost:7071/api/GetCampaigns"
    let! json = Http.AsyncRequestString(url, httpMethod = HttpMethod.Get)
    let campaigns = JsonConvert.DeserializeObject<Campaign list>(json)
    
    if campaigns.IsEmpty then
        printfn "No campaigns created"
    else
        campaigns |> List.iter (fun campaign -> printfn "%s %s" (string campaign.Id) campaign.Name)
}

let startCampaignAsync state campaignName = async {
    let url = "http://localhost:7071/api/CreateCampaign"
    let campaign = {|
        Id = Guid.NewGuid()
        Name = campaignName
        Start = DateTimeOffset.UtcNow
        End = DateTimeOffset.UtcNow.AddDays 7.0
    |}

    let json = JsonConvert.SerializeObject campaign
    let body = HttpRequestBody.TextRequest json
    let! response = Http.AsyncRequest (url, httpMethod = HttpMethod.Post, body = body)

    return! selectCampaignAsync state (string campaign.Id)
}