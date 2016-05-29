using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WebIndexer.Collections;

namespace WebIndexer.Algorithms
{
    class FloydWarshall
    {
        /* https://en.wikipedia.org/wiki/Floyd–Warshall_algorithm
        let dist be a |V| × |V| array of minimum distances initialized to ∞ (infinity)
        let next be a |V| × |V| array of vertex indices initialized to null

        procedure FloydWarshallWithPathReconstruction ()
           for each edge (u,v)
              dist[u][v] ← w(u,v)  // the weight of the edge (u,v)
              next[u][v] ← v
           for k from 1 to |V| // standard Floyd-Warshall implementation
              for i from 1 to |V|
                 for j from 1 to |V|
                    if dist[i][k] + dist[k][j] < dist[i][j] then
                       dist[i][j] ← dist[i][k] + dist[k][j]
                       next[i][j] ← next[i][k]

        procedure Path(u, v)
           if next[u][v] = null then
               return []
           path = [u]
           while u ≠ v
               u ← next[u][v]
               path.append(u)
           return path
         */
        private readonly MatrixHashTable<Uri, Uri, ulong> dist;
        private readonly MatrixHashTable<Uri, Uri, Uri> next;
        public FloydWarshall(IDictionary<Uri, WebDocument> documents)
        {
            _documents = new Dictionary<Uri, WebDocument>(documents);
            keySpace = _documents.Keys.ToArray();
            dist = new MatrixHashTable<Uri, Uri, ulong>(keySpace, keySpace);
            next = new MatrixHashTable<Uri, Uri, Uri>(keySpace,keySpace);
        }

        public async Task DoWorkAsync()
        {
            await Task.Run(() =>DoWork());
        }

        public void DoWork()
        {
            foreach (var u in keySpace)
                foreach (var v in keySpace)
                    if (_documents[u].OutLinks.Contains(v))
                    {
                        dist[u, v] = 1;
                        next[u, v] = v;
                    }
                    else
                    {
                        dist[u, v] = ulong.MaxValue;
                        next[u, v] = null;
                    }

            foreach (var k in keySpace)
                foreach (var i in keySpace)
                    foreach (var j in keySpace)
                        /*
                   if dist[i][k] + dist[k][j] < dist[i][j] then
                   dist[i][j] ← dist[i][k] + dist[k][j]
                   next[i][j] ← next[i][k]
                     */
                        if (dist[i, k] + dist[k, j] < dist[i, j])
                        {
                            dist[i, j] = dist[i, k] + dist[k, j];
                            next[i, j] = next[i, k];
                        }
        
        }

        public async Task<string> BuildPathsAsync()
        {
            var str = new StringBuilder();
            //foreach (var url1 in keySpace)
            var tasks = new ConcurrentBag<Task>();
            Parallel.ForEach(keySpace, url1 =>
            {
                foreach (var url2 in keySpace)
                {
                    tasks.Add(Task.Run(()=>AppendPath(url1, url2, str)));
                }
            });
            await Task.WhenAll(tasks);
            return str.ToString();
        }

        private void AppendPath(Uri url1, Uri url2, StringBuilder str)
        {
            var path = this.GetPath(url1, url2);
            var pathBuilder = new StringBuilder();
            foreach (var uri in path)
                pathBuilder.AppendLine(uri.ToString());

              lock (_lockObject)
            {
                str.AppendLine($"Path from {url1} to {url2}");
                str.AppendLine(pathBuilder.ToString());
            }
        }

        public string BuildPaths()
        {
            var str = new StringBuilder();
            foreach (var url1 in keySpace)
            //Parallel.ForEach(urls, url1 =>
            {
                foreach (var url2 in keySpace)
                {
                    AppendPath(url1, url2, str);
                }
            }//);
            return str.ToString();
        }

        /*
         * procedure Path(u, v)
           if next[u][v] = null then
               return []
           path = [u]
           while u ≠ v
               u ← next[u][v]
               path.append(u)
           return path
           */

        public IEnumerable<Uri> GetPath(Uri u, Uri v)
        {
            if(next[u,v]==null)
                return new List<Uri>();
            var path = new List<Uri>();
            while (u != v)
            {
                u = next[u, v];
                path.Add(u);
            }
            return path;
        }

        public ulong GetDistance(Uri u, Uri v)
        {
            return dist[u, v];
        }

        public double GetAverageDistance()
        {
            var count = 0;
            var value = 0.0;
            foreach (var u in keySpace)
                foreach (var v in keySpace)
                    if (dist[u, v] != ulong.MaxValue)
                    {
                        count++;
                        value += dist[u, v];
                    }

            return value/count;
        }

        /* The diameter d of a graph is the maximum eccentricity of any vertex in the graph.
         * That is, d it is the greatest distance between any pair of vertices or, alternatively, d = \max_{v \in V}\epsilon(v).
         * To find the diameter of a graph, first find the shortest path between each pair of vertices.
         * The greatest length of any of these paths is the diameter of the graph.*/
        public ulong GetDiameter()
        {
            ulong diameter = ulong.MinValue;
            foreach (var u in keySpace)
                foreach (var v in keySpace)
                    if (dist[u, v] != ulong.MaxValue && dist[u, v] > diameter)
                        diameter = dist[u, v];
            return diameter;
        }

        private IDictionary<Uri, WebDocument> _documents;
        private IReadOnlyCollection<Uri> keySpace;
        private object _lockObject = new object();
    }
}
