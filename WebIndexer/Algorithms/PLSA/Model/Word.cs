using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HiddenMarkov.Algorithms.PLSA.Model
{
    class Topic : IEquatable<Topic>, IEqualityComparer<Topic>
    {
        public String name { get; set; }
        public Topic(String str)
        {
            this.name = str;
        }

        public bool Equals(Topic x, Topic y)
        {
            return x.name.Equals(y.name);
        }

        public int GetHashCode(Topic obj)
        {
            return obj.name.GetHashCode();
        }

        public bool Equals(Topic other)
        {
            return this.name.Equals(other.name);
        }
    }


}
