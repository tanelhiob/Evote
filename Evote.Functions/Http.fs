module Http

open Microsoft.AspNetCore.Http
open Microsoft.WindowsAzure.Storage.Table
open Microsoft.Extensions.Logging
open Microsoft.Azure.WebJobs
open Microsoft.Azure.WebJobs.Extensions.Http
open Newtonsoft.Json
open Microsoft.AspNetCore.Mvc
open System
open Data
open Domain
open System.Net

let private createInternalServerErrorObjectResult message =
    let objectResult = new ObjectResult()
    objectResult.StatusCode <- new Nullable<int>(int HttpStatusCode.InternalServerError)
    objectResult.Value <- message
    objectResult

let private getPostContentAsync<'TResult> (httpRequest: HttpRequest) = async {
    let! json = httpRequest.ReadAsStringAsync() |> Async.AwaitTask
    let content = JsonConvert.DeserializeObject<'TResult>(json)
    return content
}

[<FunctionName("CastVote")>]
let castVote ([<HttpTrigger(AuthorizationLevel.Anonymous, "post")>] httpRequest: HttpRequest,
                [<Table("evotevotes")>] votesTable: CloudTable,
                [<Table("evotecampaigns")>] campaignsTable: CloudTable,
                logger: ILogger) =
    async {
        let! vote = getPostContentAsync httpRequest
        let! result = castVoteAsync vote (saveVoteAsync votesTable) (loadCampaignAsync campaignsTable)
        match result with
        | Ok message ->
            logger.LogInformation message
            return new OkResult() :> ActionResult
        | Error error ->
            logger.LogError error
            return createInternalServerErrorObjectResult error :> ActionResult
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
        let! campaigns = loadPublicActiveCampaignsAsync table
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
        let! campaignResult = loadCampaignAsync table campaignId
        
        return
            match campaignResult with
            | Ok campaign -> new OkObjectResult(campaign) :> ActionResult
            | Error error -> createInternalServerErrorObjectResult error :> ActionResult
    }
    |> Async.StartAsTask