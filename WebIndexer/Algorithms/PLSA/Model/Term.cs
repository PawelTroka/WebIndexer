using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HiddenMarkov.Algorithms.PLSA.Model
{
    class Term : IEqualityComparer<Term>, IEquatable<Term>
    {
        public String word { get; set; }
        public Term(String str)
        {
            this.word = str;
        }

        public bool Equals(Term x, Term y)
        {
            return x.word.Equals(y.word);
        }

        public int GetHashCode(Term obj)
        {
            return obj.word.GetHashCode();
        }

        public bool Equals(Term other)
        {
            return this.word.Equals(other.word);
        }

        public override string ToString()
        {
            return word;
        }
    }
}
