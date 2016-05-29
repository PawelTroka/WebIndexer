using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WebIndexer.Algorithms
{
    class PageRank
    {
        private IDictionary<Uri,WebDocument> _documents;
        private IReadOnlyCollection<Uri> _urlSpace;
        public PageRank(IDictionary<Uri, WebDocument> documents)
        {
            _documents = documents;
            _urlSpace = _documents.Keys.ToArray();
        }

        public double DampingFactor { get; set; } = 0.85;//1.0;// 0.85;

        public int MaxSteps { get; set; } = 10;

        public double Convergence { get; set; } = 1e-4;
        private int N { get { return _urlSpace.Count; } }

        private readonly Dictionary<Uri, double> _oldPageRankValues=new Dictionary<Uri, double>();

        public void DoWork()
        {
            foreach (var uri in _urlSpace)
            {
                _documents[uri].PageRank = 1.0/ N;
                _oldPageRankValues[uri] = double.MaxValue/100;
            }

            while(_oldPageRankValues.All(kvp => Math.Abs(kvp.Value-_documents[kvp.Key].PageRank)>Convergence))
            foreach (var uri1 in _urlSpace)
            {
                _oldPageRankValues[uri1] = _documents[uri1].PageRank;

                _documents[uri1].PageRank = (1 - DampingFactor)/N;
                var sum = 0.0;
                foreach (var uri2 in _urlSpace)
                {
                    if (_documents[uri2].OutLinks.Contains(uri1))
                        sum += _documents[uri2].PageRank/_documents[uri2].Outdegree;
                }
                _documents[uri1].PageRank += DampingFactor*sum;
            }
        }
    }
}
