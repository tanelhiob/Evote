namespace Evote.Functions

open Microsoft.WindowsAzure.Storage.Table
open System
open FSharp.Control
open Microsoft.WindowsAzure.Storage.Table

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
    
    let saveCampaignAsync (table: CloudTable) (campaign: Campaign) = async {
        let rowKey = campaign.Id |> string
        let partitionKey = ""
        let campaignAttributes = Map [
            ("Name", new EntityProperty(campaign.Name))
            ("Start", new EntityProperty(new Nullable<DateTimeOffset>(campaign.Start)))
            ("End", new EntityProperty(new Nullable<DateTimeOffset>(campaign.End)))
        ]

        let campaignEntity = new DynamicTableEntity(partitionKey, rowKey, null, campaignAttributes)

        let operation = TableOperation.InsertOrReplace(campaignEntity)
        return! table.ExecuteAsync(operation) |> Async.AwaitTask
    }

    let saveVoteAsync (table: CloudTable) (vote: Vote) = async {
        let rowKey = vote.Voter
        let partitionKey = vote.CampaignId |> string
        let voterAttributes = Map [
            ("VoterToken", new EntityProperty(vote.VoterToken))
            ("Choice", new EntityProperty(vote.Choice))
            ("ChoiceToken", new EntityProperty(vote.ChoiceToken))
        ]

        let voterEntity = new DynamicTableEntity(partitionKey, rowKey, null, voterAttributes)

        let operation = TableOperation.InsertOrReplace(voterEntity)
        return! table.ExecuteAsync(operation) |> Async.AwaitTask
    }
    
    let loadCampaignVotesAsync (table: CloudTable) (campaignId: Guid) =  async {
        let tableQuery = (new TableQuery()).Where(TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, campaignId.ToString()))
        
        let executeQueryAsync (token: TableContinuationToken) = async {
            match token with
            | null -> return None
            | _ -> let! segmentedResult = table.ExecuteQuerySegmentedAsync(tableQuery, token) |> Async.AwaitTask
                   return Some (segmentedResult.Results, segmentedResult.ContinuationToken)
        }
        
        let toVote (entity: DynamicTableEntity) =         
            {
                CampaignId = Guid.Parse entity.PartitionKey
                Voter = entity.RowKey
                VoterToken = entity.Properties.["VoterToken"].StringValue
                Choice = entity.Properties.["Choice"].StringValue
                ChoiceToken = entity.Properties.["ChoiceToken"].StringValue
            }

        return!
            AsyncSeq.unfoldAsync executeQueryAsync (new TableContinuationToken())
            |> AsyncSeq.concatSeq
            |> AsyncSeq.map toVote
            |> AsyncSeq.toListAsync
    }

    let loadCampaignsAsync (table: CloudTable) = async {
        let tableQuery = new TableQuery()

        let executeQueryAsync (token: TableContinuationToken) = async {
            match token with
            | null -> return None
            | _ -> let! segmentedResult = table.ExecuteQuerySegmentedAsync(tableQuery, token) |> Async.AwaitTask
                   return Some (segmentedResult.Results, segmentedResult.ContinuationToken)
        }

        let toCampaign (entity: DynamicTableEntity) =
            {
                Id = Guid.Parse entity.RowKey
                Name = entity.Properties.["Name"].StringValue
                Start = entity.Properties.["Start"].DateTimeOffsetValue.Value
                End = entity.Properties.["End"].DateTimeOffsetValue.Value
            }

        return!
            AsyncSeq.unfoldAsync executeQueryAsync (new TableContinuationToken())
            |> AsyncSeq.concatSeq
            |> AsyncSeq.map toCampaign
            |> AsyncSeq.toListAsync
    }

    let loadCampaignAsync (table: CloudTable) (campaignId: Guid) = async {
        let retrievalOperation = TableOperation.Retrieve("", string campaignId)
        let! tableResult = table.ExecuteAsync(retrievalOperation) |> Async.AwaitTask

        if tableResult.HttpStatusCode <> 200 then
            raise (new Exception("Failed to retrieve campaign"))

        let dynamicTableEntity = tableResult.Result :?> DynamicTableEntity

        let campaign = {
            Id = Guid.Parse dynamicTableEntity.RowKey
            Name = dynamicTableEntity.Properties.["Name"].StringValue
            Start = dynamicTableEntity.Properties.["Start"].DateTimeOffsetValue.Value
            End = dynamicTableEntity.Properties.["End"].DateTimeOffsetValue.Value
        }

        return campaign
    }