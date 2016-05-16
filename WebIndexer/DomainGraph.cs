using System;
using System.Collections.Concurrent;

namespace WebIndexer
{
    internal class DomainGraph
    {
        private readonly ConcurrentDictionary<Uri, ConcurrentDictionary<Uri, uint>> _graph;

        public DomainGraph()
        {
            _graph = new ConcurrentDictionary<Uri, ConcurrentDictionary<Uri, uint>>();
        }

        public uint this[Uri uri1, Uri uri2]
        {
            get
            {
                if (_graph.ContainsKey(uri1) && _graph[uri1].ContainsKey(uri2))
                    return _graph[uri1][uri2];
                return 0;
            }
            set
            {
                if (!_graph.ContainsKey(uri1))
                    _graph.TryAdd(uri1, new ConcurrentDictionary<Uri, uint>());
                // if(!_graph[uri1].ContainsKey(uri2))


                //if (_graph.ContainsKey(uri1) && _graph[uri1].ContainsKey(uri2))
                _graph[uri1][uri2] = value;
            }
        }
    }
}