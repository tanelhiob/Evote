module Domain

open System

[<CLIMutable>]
type Campaign = {
    Id: Guid
    Name: string
    Start: DateTimeOffset
    End: DateTimeOffset
    IsPublic: bool
    Choices: string list option
}

[<CLIMutable>]
type Vote = {
    CampaignId: Guid
    Voter: string
    VoterToken: string
    Choice: string
    ChoiceToken: string
}

let createCampaignAsync (campaign: Campaign)
                        (saveCampaignAsync: Campaign -> Async<unit>) = async {

    let isCampaignValid campaign =
        campaign.Start <= campaign.End

    if isCampaignValid campaign then
        do! saveCampaignAsync campaign
        return Ok "campaign successfully created"
    else
        return Error "invalid campaign"
} 

let castVoteAsync (vote: Vote)
                  (saveVoteAsync: Vote -> Async<unit>)
                  (loadCampaignAsync: Guid -> Async<Result<Campaign, string>>) = async {

    let isCampaignActive campaign =
        campaign.Start <= DateTimeOffset.UtcNow && campaign.End > DateTimeOffset.UtcNow
 
    let campaignHasChoice campaign vote =
        match campaign.Choices with
        | Some choices -> choices |> List.map (fun choice -> choice.ToLower()) |> List.contains (vote.Choice.ToLower())
        | None -> true

    let! campaignResult = loadCampaignAsync vote.CampaignId
    let choiceValidationResult = campaignResult |> Result.bind (fun campaign -> if campaignHasChoice campaign vote then Ok campaign else Error "campaign doesn't have selected choice")
    let activeValidationResult = choiceValidationResult |> Result.bind (fun campaign -> if isCampaignActive campaign then Ok campaign else Error "campaign is not active")

    match activeValidationResult with
    | Ok _ -> do! saveVoteAsync vote
              return Ok "voting successful"
    | Error error -> return Error error
}