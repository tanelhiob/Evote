namespace Evote.Functions

module Http =

    open Microsoft.AspNetCore.Http
    open Microsoft.WindowsAzure.Storage.Table
    open Microsoft.Extensions.Logging
    open Microsoft.Azure.WebJobs
    open Microsoft.Azure.WebJobs.Extensions.Http
    open Newtonsoft.Json
    open Microsoft.AspNetCore.Mvc
    open System
    open Domain

    let getPostContentAsync<'TResult> (httpRequest: HttpRequest) = async {
        let! json = httpRequest.ReadAsStringAsync() |> Async.AwaitTask
        let content = JsonConvert.DeserializeObject<'TResult>(json)
        return content
    }

    [<FunctionName("CastVote")>]
    let castVote ([<HttpTrigger(AuthorizationLevel.Anonymous, "post")>] httpRequest: HttpRequest,
                  [<Table("evotevotes")>] table: CloudTable,               
                  logger: ILogger) =
        async {
            let! vote = getPostContentAsync httpRequest
            do! saveVoteAsync table vote
            return new OkResult()
        }
        |> Async.StartAsTask

    [<FunctionName("GetVotes")>]
    let getVotes ([<HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "GetVotes/{campaignIdString}")>] httpRequest: HttpRequest,
                  [<Table("evotevotes")>] table: CloudTable,
                  campaignIdString: string,
                  logger: ILogger) =
        async {
            let campaignId = Guid.Parse campaignIdString
            let! votes = loadCampaignVotesAsync table campaignId

            let voters, choices =
                votes
                |> List.map (fun vote -> {| Name = vote.Voter; Token = vote.VoterToken |}, {| Value = vote.Choice; Token = vote.ChoiceToken |})
                |> List.fold (fun (voters, choices) (voter, choice) -> voter::voters, choice::choices) ([], [])

            let response = {|
                Voters = voters |> List.sortBy (fun voter -> voter.Name)
                Choices = choices |> List.sortBy (fun choice -> choice.Value)
            |}

            return new OkObjectResult(response)
        }
        |> Async.StartAsTask

    [<FunctionName("CreateCampaign")>]
    let createCampaign ([<HttpTrigger(AuthorizationLevel.Anonymous, "post")>] httpRequest: HttpRequest,
                        [<Table("evotecampaigns")>] table: CloudTable,
                        logger: ILogger) =
        async {
            let! campaign = getPostContentAsync httpRequest
            do! saveCampaignAsync table campaign
            return new OkResult()
        }
        |> Async.StartAsTask

    [<FunctionName("GetCampaigns")>]
    let getCampaigns ([<HttpTrigger(AuthorizationLevel.Anonymous, "get")>] httpRequest: HttpRequest,
                      [<Table("evotecampaigns")>] table: CloudTable,
                      logger: ILogger) =
        async {
            let! campaigns = loadCampaignsAsync table
            return new OkObjectResult(campaigns)
        }
        |> Async.StartAsTask 

    [<FunctionName("GetCampaign")>]
    let getCampaign ([<HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "GetCampaign/{campaignIdString}")>] httpRequest: HttpRequest,
                     [<Table("evotecampaigns")>] table: CloudTable,
                     campaignIdString: string,
                     logger: ILogger) =
        async {
            let campaignId = Guid.Parse campaignIdString
            let! campaign = loadCampaignAsync table campaignId
           
            return new OkObjectResult(campaign)
        }
        |> Async.StartAsTask