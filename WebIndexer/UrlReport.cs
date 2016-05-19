using System;

namespace WebIndexer
{
    internal class UrlReport
    {
        public UrlReport(Uri url, UrlStatus urlStatus)
        {
            Url = url;
            Status = urlStatus;
        }

        public UrlReport(string message)
        {
            this.Message = message;
            Status=UrlStatus.Info;
        }

        public UrlStatus Status { get; }
        public Uri Url { get; }

        public string Message { get; }
        public override string ToString()
        {
            return $"{Status}: {Url}";
        }
    }
}