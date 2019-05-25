using System.Collections.Generic;
using System.Linq;

namespace Evote.Client.Models
{
    public class Option<T>
    {
        public string Case { get; set; }
        public IList<T> Fields { get; set; }
        public T Value => Fields.First();
    }
}
