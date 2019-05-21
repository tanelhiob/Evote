namespace Evote.Functions

open Microsoft.WindowsAzure.Storage.Table
open System
open FSharp.Control

module Domain =

    [<CLIMutable>]
    type Campaign = {
        Id: Guid
        Name: string
        Start: DateTimeOffset
        End: DateTimeOffset
    }

    [<CLIMutable>]
    type Vote = {
        CampaignId: Guid
        Voter: string
        VoterToken: string
        Choice: string
        ChoiceToken: string
    }
    
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

    let private loadEntitiesAsync<'TResult> (table: CloudTable) (partitionKey: string) (mapper: DynamicTableEntity -> 'TResult) = async {
        let query = new TableQuery()
        query.FilterString <- TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, partitionKey)

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
            return None
        else
            let dynamicTableEntity = result.Result :?> DynamicTableEntity
            let result = mapper dynamicTableEntity
            return Some result
    }

    let saveCampaignAsync (table: CloudTable) (campaign: Campaign) = async {
        let rowKey = campaign.Id |> string
        let partitionKey = ""
        let properties = Map [
            ("Name", new EntityProperty(campaign.Name))
            ("Start", new EntityProperty(new Nullable<DateTimeOffset>(campaign.Start)))
            ("End", new EntityProperty(new Nullable<DateTimeOffset>(campaign.End)))
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

        return! loadEntitiesAsync table (string campaignId) mapper
    }

    let loadCampaignsAsync (table: CloudTable) = async {
        let mapper (entity: DynamicTableEntity) =
            {
                Id = Guid.Parse entity.RowKey
                Name = entity.Properties.["Name"].StringValue
                Start = entity.Properties.["Start"].DateTimeOffsetValue.Value
                End = entity.Properties.["End"].DateTimeOffsetValue.Value
            }

        return! loadEntitiesAsync table "" mapper
    }

    let loadCampaignAsync (table: CloudTable) (campaignId: Guid) = async {
        let mapper (entity: DynamicTableEntity) =
            {
                Id = Guid.Parse entity.RowKey
                Name = entity.Properties.["Name"].StringValue
                Start = entity.Properties.["Start"].DateTimeOffsetValue.Value
                End = entity.Properties.["End"].DateTimeOffsetValue.Value
            }

        return! loadEntityAsync table (string campaignId) "" mapper
    }