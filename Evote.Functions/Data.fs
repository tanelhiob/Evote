module Data

open Microsoft.WindowsAzure.Storage.Table
open System
open FSharp.Control
open Newtonsoft.Json
open Domain
    
let private saveEntityAsync (table: CloudTable) (rowKey: string) (partitionKey: string) (properties: Map<string, EntityProperty>) = async {
    let dynamicTableEntity = new DynamicTableEntity(partitionKey, rowKey, null, properties)
    let operation = TableOperation.InsertOrReplace(dynamicTableEntity)
    do! table.ExecuteAsync(operation) |> Async.AwaitTask |> Async.Ignore
}
    
let private executeQueryAsync (table: CloudTable) (query: TableQuery) (token: TableContinuationToken) = async {
    if token = null then
        return None
    else
        let! segmentedResult = table.ExecuteQuerySegmentedAsync(query, token) |> Async.AwaitTask
        return Some (segmentedResult.Results, segmentedResult.ContinuationToken)
}

let private loadEntitiesAsync<'TResult> (table: CloudTable) (partitionKey: string) (mapper: DynamicTableEntity -> 'TResult) (optionalFilter: string option) = async {
    let query = new TableQuery()

    let partitionFilter = TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, partitionKey) 
    let combinedFilter = optionalFilter |> Option.bind (fun filter -> TableQuery.CombineFilters(partitionFilter, TableOperators.And, filter) |> Some)
    query.FilterString <- combinedFilter |> Option.defaultValue partitionFilter

    return!
        AsyncSeq.unfoldAsync (executeQueryAsync table query) (new TableContinuationToken())
        |> AsyncSeq.concatSeq
        |> AsyncSeq.map mapper
        |> AsyncSeq.toListAsync
}

let private loadEntityAsync<'TResult> (table: CloudTable) (rowKey: string) (partitionKey: string) (mapper: DynamicTableEntity -> 'TResult) = async {
    let operation = TableOperation.Retrieve(partitionKey, rowKey)
    let! result = table.ExecuteAsync(operation) |> Async.AwaitTask

    if result.Result = null then
        let entityType = typeof<'TResult>
        return Error (sprintf "entity of type %s by (%s-%s) was not found" entityType.Name rowKey partitionKey)
    else
        let dynamicTableEntity = result.Result :?> DynamicTableEntity
        let result = mapper dynamicTableEntity
        return Ok result
}

let private mapCampaignFromDynamicTableEntity (entity: DynamicTableEntity) = {
    Id = Guid.Parse entity.RowKey
    Name = entity.Properties.["Name"].StringValue
    Start = entity.Properties.["Start"].DateTimeOffsetValue.Value
    End = entity.Properties.["End"].DateTimeOffsetValue.Value
    IsPublic = entity.Properties.["IsPublic"].BooleanValue.Value
    Choices = entity.Properties.["Choices"].StringValue |> JsonConvert.DeserializeObject<string list option>
}

let saveCampaignAsync (table: CloudTable) (campaign: Campaign) = async {
    let rowKey = campaign.Id |> string
    let partitionKey = ""
    let properties = Map [
        ("Name", new EntityProperty(campaign.Name))
        ("Start", new EntityProperty(new Nullable<DateTimeOffset>(campaign.Start)))
        ("End", new EntityProperty(new Nullable<DateTimeOffset>(campaign.End)))
        ("IsPublic", new EntityProperty(new Nullable<bool>(campaign.IsPublic)))
        ("Choices", new EntityProperty(JsonConvert.SerializeObject(campaign.Choices)))
    ]

    do! saveEntityAsync table rowKey partitionKey properties
}

let saveVoteAsync (table: CloudTable) (vote: Vote) = async {
    let rowKey = vote.Voter
    let partitionKey = vote.CampaignId |> string
    let properties = Map [
        ("VoterToken", new EntityProperty(vote.VoterToken))
        ("Choice", new EntityProperty(vote.Choice))
        ("ChoiceToken", new EntityProperty(vote.ChoiceToken))
    ]

    do! saveEntityAsync table rowKey partitionKey properties
}
    
let loadCampaignVotesAsync (table: CloudTable) (campaignId: Guid) =  async {
    let mapper (entity: DynamicTableEntity) =         
        {
            CampaignId = Guid.Parse entity.PartitionKey
            Voter = entity.RowKey
            VoterToken = entity.Properties.["VoterToken"].StringValue
            Choice = entity.Properties.["Choice"].StringValue
            ChoiceToken = entity.Properties.["ChoiceToken"].StringValue
        }

    return! loadEntitiesAsync table (string campaignId) mapper None
}

let loadPublicActiveCampaignsAsync (table: CloudTable) = async {
    let publicFilter = TableQuery.GenerateFilterConditionForBool("IsPublic", QueryComparisons.Equal, true)
    let hasStartedFilter = TableQuery.GenerateFilterConditionForDate("Start", QueryComparisons.LessThanOrEqual, DateTimeOffset.UtcNow)
    let hasntEndedFilter = TableQuery.GenerateFilterConditionForDate("End", QueryComparisons.GreaterThan, DateTimeOffset.UtcNow)
    let activeFilter = TableQuery.CombineFilters(hasStartedFilter, TableOperators.And, hasntEndedFilter)            
    let combinedFilter = TableQuery.CombineFilters(publicFilter, TableOperators.And, activeFilter)

    return! loadEntitiesAsync table "" mapCampaignFromDynamicTableEntity (Some combinedFilter)
}

let loadCampaignAsync (table: CloudTable) (campaignId: Guid) = async {
    return! loadEntityAsync table (string campaignId) "" mapCampaignFromDynamicTableEntity
}