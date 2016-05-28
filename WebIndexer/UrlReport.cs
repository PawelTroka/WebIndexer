using System;

namespace WebIndexer
{
    internal class UrlReport
    {
        public UrlReport(Uri url, ReportStatus reportStatus)
        {
            Url = url;
            Status = reportStatus;
        }

        public UrlReport(string message)
        {
            this.Message = message;
            Status=ReportStatus.Information;
        }

        public ReportStatus Status { get; }
        public Uri Url { get; }

        public string Message { get; }
        public override string ToString()
        {
            return $"{Status}: {Url}";
        }
    }
}