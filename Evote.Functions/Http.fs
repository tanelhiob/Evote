namespace Evote.Functions

module Http =
    open Microsoft.Azure.WebJobs
    open Microsoft.AspNetCore.Http
    open Microsoft.Extensions.Logging
    open Microsoft.WindowsAzure.Storage.Table
    open Microsoft.Azure.WebJobs.Extensions.Http
    open Newtonsoft.Json
    open System
    open Microsoft.AspNetCore.Mvc
    open System.Threading.Tasks
    
    [<CLIMutable>]
    type Vote = {
        Voter: string
        VoterToken: string
        Choice: string
        ChoiceToken: string
    }

    let saveVoter (table: CloudTable) vote = async {
        let voterAttributes = Map [("VoterToken", new EntityProperty(vote.VoterToken))]
        let voterEntity = new DynamicTableEntity("", vote.Voter, null, voterAttributes)

        let operation = TableOperation.Insert(voterEntity)
        return! table.ExecuteAsync(operation) |> Async.AwaitTask
    }
        
    let saveChoice (table: CloudTable) vote = async {
        let id = Guid.NewGuid().ToString()
        let choiceAttributes = Map [
            ("ChoiceToken", new EntityProperty(vote.ChoiceToken))
            ("Choice", new EntityProperty(vote.Choice))
        ]
        let choiceEntity = new DynamicTableEntity("", id, null, choiceAttributes)

        let operation = TableOperation.Insert(choiceEntity)
        return! table.ExecuteAsync(operation) |> Async.AwaitTask
    }

    [<FunctionName("CastVote")>]
    let castVote ([<HttpTrigger(AuthorizationLevel.Anonymous, "post")>] httpRequest: HttpRequest,
                  [<Table("choices")>] choicesTable: CloudTable,
                  [<Table("voters")>] votersTable: CloudTable,
                  logger: ILogger) =
        async {
                  
            let! json = httpRequest.ReadAsStringAsync() |> Async.AwaitTask
            let vote = JsonConvert.DeserializeObject<Vote>(json)

            let! tableResults =  Async.Parallel [
                saveVoter votersTable vote
                saveChoice choicesTable vote
            ]

            return new OkResult()
        }
        |> Async.StartAsTask
    
    let loadTable table = async {
        
        ignore()
    }

    type Choice = {
        Value: string
        Token: string
    }

    type Voter = {
        Name: string
        Token: string
    }

    [<FunctionName("GetVotes")>]
    let getVotes ([<HttpTrigger(AuthorizationLevel.Anonymous, "get")>] httpRequest: HttpRequest,
                  [<Table("choices")>] choicesTable: CloudTable,
                  [<Table("voters")>] votersTable: CloudTable,
                  logger: ILogger) =
        async {
             let! choiceEntities = loadTable choicesTable
             let! voterEntities = loadTable votersTable

             let result = [choiceEntities; voterEntities]

             return new OkObjectResult(result)
        }
        |> Async.StartAsTask