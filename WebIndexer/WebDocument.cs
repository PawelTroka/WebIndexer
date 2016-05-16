using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace WebIndexer
{
    internal class WebDocument
    {
        public double DownloadTime { get; set; }

        public int Indegree
        {
            get { return InLinks.Count; }
        }

        public int Outdegree
        {
            get { return OutLinks.Count; }
        }

        public bool Analyzed { get; set; }

        public Uri AbsoluteUrl { get; set; }
        public ConcurrentBag<Uri> OutLinks { get; set; } = new ConcurrentBag<Uri>();
        public ConcurrentBag<Uri> InLinks { get; set; } = new ConcurrentBag<Uri>();
    }
}