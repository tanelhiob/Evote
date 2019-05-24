module Handlers

open System
open Newtonsoft.Json
open FSharp.Data
open FSharp.Data.HttpContentTypes

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
    Choices: string list option
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

let private getResponseResult (httpResponse: HttpResponse) =
    if httpResponse.StatusCode >= 200 && httpResponse.StatusCode < 300 then
        match httpResponse.Body with
        | HttpResponseBody.Text text -> Ok text
        | _ -> Ok "success"
    else Error "error"
            
let login state name =
    printfn "You are now %s" name
    let login = {
        Name = name
        Secret = Guid.NewGuid().ToString() }
    { state with Login = Some login }
               
let voteAsync state choice = async {
    match state with
    | { Login = Some login; Campaign = Some campaign } ->
        let vote = {
            CampaignId = campaign.Id
            Voter = login.Name
            VoterToken = login.Secret
            Choice = choice
            ChoiceToken = login.Secret
        }

        if state.Vote.IsSome && state.Vote.Value.CampaignId = campaign.Id then
            printfn "Changing vote from %s to %s" state.Vote.Value.Choice choice

        let voteJson = JsonConvert.SerializeObject(vote)
        let body = HttpRequestBody.TextRequest voteJson
        let url = "http://localhost:7071/api/CastVote"

        let! response = Http.AsyncRequest(url, httpMethod = "POST", body = body)

        printfn "%A" response.Headers

        match getResponseResult response with
        | Ok text -> printfn "%s" text
                     return { state with Vote = Some vote }
        | Error text -> printfn "Voting failed - %s" text
                        return state
    | _ ->
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

let selectCampaignAsync state (campaignId: Guid) = async {
    let url = sprintf "http://localhost:7071/api/GetCampaign/%A" campaignId
    let! json = Http.AsyncRequestString (url, httpMethod = HttpMethod.Get)
    let campaign = JsonConvert.DeserializeObject<Campaign>(json)
    
    printfn "Selected campaign %s" campaign.Name
    printfn "%s" (campaign.Choices |> Option.bind (String.concat "; " >> Some) |> Option.defaultValue "open choices")

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

let startCampaignAsync state campaignName (campaignChoices: string list) = async {
    let url = "http://localhost:7071/api/CreateCampaign"
    let campaign = {|
        Id = Guid.NewGuid()
        Name = campaignName
        Start = DateTimeOffset.UtcNow
        End = DateTimeOffset.UtcNow.AddDays 7.0
        IsPublic = true
        Choices = campaignChoices |> (function | [] -> None | choices -> Some choices)
    |}

    let json = JsonConvert.SerializeObject campaign
    let body = HttpRequestBody.TextRequest json
    let! response = Http.AsyncRequest (url, httpMethod = HttpMethod.Post, body = body)

    return! selectCampaignAsync state campaign.Id
}