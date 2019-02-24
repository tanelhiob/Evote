open System
open System.Net.Http
open Newtonsoft.Json
open System.Text

[<CLIMutable>]
type Vote = {
    Voter: string
    VoterToken: string
    Choice: string
    ChoiceToken: string
}

[<EntryPoint>]
let main _ =
    
    use httpClient = new HttpClient()

    //let token = Guid.NewGuid().ToString()
    //let vote = {
    //    Voter = "Tanel"
    //    VoterToken = token
    //    Choice = "Andra"
    //    ChoiceToken = token
    //}

    //let json = JsonConvert.SerializeObject(vote)
    //let content = new StringContent(json, Encoding.UTF8, "application/json")
    //let response = httpClient.PostAsync ("http://localhost:7071/api/CastVote", content) |> Async.AwaitTask |> Async.RunSynchronously
    //response.EnsureSuccessStatusCode() |> ignore



    0