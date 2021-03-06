namespace WebIndexer
{
    public enum ReportStatus
    {
        UrlProcessed,
        UrlError,
        UrlSkippedExternal,
        UrlSkippedFile,
        Information,
        ShortestPaths,
        ProbabilisticLSA,

        PLSATermsByTopic,
        PLSATopicByTerms,

        PLSATopicByDocument,
        PLSADocumentByTopic,
    }
}