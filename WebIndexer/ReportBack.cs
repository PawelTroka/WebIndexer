using System;

namespace WebIndexer
{
    internal class ReportBack
    {
        public ReportBack(Uri url, ReportStatus reportStatus)
        {
            Url = url;
            Status = reportStatus;
        }

        public ReportBack(string message)
        {
            this.Message = message;
            Status=ReportStatus.Information;
        }

        public ReportBack(string message, ReportStatus reportStatus)
        {
            this.Message = message;
            Status = reportStatus;
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