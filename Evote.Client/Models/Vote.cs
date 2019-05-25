using System;

namespace Evote.Client.Models
{
    public class Vote
    {
        public Guid CampaignId { get; set; }
        public string Voter { get; set; }
        public string VoterToken { get; set; }
        public string Choice { get; set; }
        public string ChoiceToken { get; set; }
    }
}
