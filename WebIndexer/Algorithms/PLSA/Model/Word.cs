using System;
using System.Collections.Generic;

namespace WebIndexer.Algorithms.PLSA.Model
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

        public override string ToString()
        {
            return name;
        }
    }


}
