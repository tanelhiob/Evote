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

    let save (table: CloudTable) (vote: Vote) = async {
        let id = vote.Voter
        let voterAttributes = Map [
            ("VoterToken", new EntityProperty(vote.VoterToken))
            ("Choice", new EntityProperty(vote.Choice))
            ("ChoiceToken", new EntityProperty(vote.ChoiceToken))
        ]

        let voterEntity = new DynamicTableEntity("", id, null, voterAttributes)

        let operation = TableOperation.InsertOrReplace(voterEntity)
        return! table.ExecuteAsync(operation) |> Async.AwaitTask
    }

    [<FunctionName("CastVote")>]
    let castVote ([<HttpTrigger(AuthorizationLevel.Anonymous, "post")>] httpRequest: HttpRequest,
                  [<Table("votes")>] table: CloudTable,               
                  logger: ILogger) =
        async {
            let! json = httpRequest.ReadAsStringAsync() |> Async.AwaitTask
            let vote = JsonConvert.DeserializeObject<Vote>(json)
            let! tableResults = save table vote

            return new OkResult()
        }
        |> Async.StartAsTask
    
    let loadTable (table: CloudTable) = 
        let tableQuery = new TableQuery()
        
        let executeQuery (token: TableContinuationToken) =
            match token with 
            | null -> None
            | _ -> let segmentedResult = table.ExecuteQuerySegmentedAsync(tableQuery, token) |> Async.AwaitTask |> Async.RunSynchronously
                   Some (segmentedResult.Results, segmentedResult.ContinuationToken)
        
        let toVote (entity: DynamicTableEntity) =         
            {
                Voter = entity.RowKey
                VoterToken = entity.Properties.["VoterToken"].StringValue
                Choice = entity.Properties.["Choice"].StringValue
                ChoiceToken = entity.Properties.["ChoiceToken"].StringValue
            }

        Seq.unfold executeQuery (new TableContinuationToken())
        |> Seq.concat
        |> Seq.map toVote
        |> Seq.toList

    type Choice = {
        Value: string
        Token: string
    }
    let Choice vote = { Value = vote.Choice; Token = vote.ChoiceToken }

    type Voter = {
        Name: string
        Token: string
    }
    let Voter vote = { Name = vote.Voter; Token = vote.VoterToken }
        

    [<FunctionName("GetVotes")>]
    let getVotes ([<HttpTrigger(AuthorizationLevel.Anonymous, "get")>] httpRequest: HttpRequest,
                  [<Table("votes")>] table: CloudTable,
                  logger: ILogger) =
        async {
             let votes = loadTable table
             
             let voters, choices =
                votes
                |> List.fold (fun (voters, choices) vote -> Voter vote :: voters, Choice vote:: choices ) ([], [])

             return new OkObjectResult(voters |> List.sortBy (fun x -> x.Name), choices |> List.sortBy (fun x -> x.Value))
        }
        |> Async.StartAsTask