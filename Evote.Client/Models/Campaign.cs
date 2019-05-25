using System;
using System.Collections.Generic;

namespace Evote.Client.Models
{
    public class Campaign
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public DateTimeOffset Start { get; set; }
        public DateTimeOffset End { get; set; }
        public bool IsPublic { get; set; }
        public Option<IList<string>> Choices { get; set; }
    }
}
