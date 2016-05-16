using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace WebIndexer
{
    internal class WebCrawler
    {

        private readonly ConcurrentDictionary<Uri, WebDocument> _documents =
            new ConcurrentDictionary<Uri, WebDocument>();

        private readonly DomainGraph _domainGraph = new DomainGraph();
        private readonly ConcurrentBag<Uri> _invalidUrls = new ConcurrentBag<Uri>();
        private readonly IProgress<UrlReport> _progressHandler;
        private Uri _domain;

        private readonly Regex aHrefRegex = new Regex(@"<a\s+(?:[^>]*?\s+)?href=""([^""]*)""", RegexOptions.Compiled);

        public WebCrawler(IProgress<UrlReport> progressHandler)
        {
            _progressHandler = progressHandler;
        }

        public IEnumerable<WebDocument> Documents
        {
            get
            {
                foreach (var webDocument in _documents)
                {
                    yield return webDocument.Value;
                }
            }
        }


        public Task Analyze(string domain)
        {
            _domain = new Uri(domain);
            // _domain = GetValidUrlOrNull(domain);
            //if (_domain == null)
            //  return Task.Delay(1);
            _documents[_domain] = new WebDocument() {AbsoluteUrl = _domain};
            return Task.Run(() => AnalyzeUrl(_domain));
        }


        private void AnalyzeUrl(Uri url)
        {
            _progressHandler.Report(new UrlReport(url, UrlStatus.Processed)); //_progressHandler.Report($"{url} - OK!");
            _documents[url].Analyzed = true;
            var stopwatch = new Stopwatch();

            //1. download document and measure time
            stopwatch.Start();
            var str = GetDocument(url);
            stopwatch.Stop();
            _documents[url].DownloadTime = stopwatch.Elapsed.TotalMilliseconds;


            //2. save document
            //TODO: save



            var matches = aHrefRegex.Matches(str).Cast<Match>();
            //3. analyze document
           // foreach (Match match in matches)
            Parallel.ForEach(matches, match =>
            {
                var linkUrl = GetValidUrlOrNull(match.Groups[1].Value);

                if (linkUrl == null || _invalidUrls.Contains(linkUrl)) return;

                if (!IsUrlOnline(linkUrl))
                {
                    _invalidUrls.Add(linkUrl);
                    _progressHandler.Report(new UrlReport(url, UrlStatus.Error));
                        //_progressHandler.Report($"{url} - OK!");
                    return;
                }

                _domainGraph[url, linkUrl]++;
                _documents[url].OutLinks.Add(linkUrl);

                if (!_documents.ContainsKey(linkUrl))
                    _documents.TryAdd(linkUrl, new WebDocument() {AbsoluteUrl = linkUrl});

                _documents[linkUrl].InLinks.Add(url);

                //4. start analyze for all nested links
                if (!_documents[linkUrl].Analyzed)
                    AnalyzeUrl(linkUrl);
            });


        }


        private Uri GetValidUrlOrNull(string urlStr)
        {
            Uri linkUrl;

            if (Uri.IsWellFormedUriString(urlStr, UriKind.Relative))
                linkUrl = new Uri(_domain, urlStr);
            else if (Uri.IsWellFormedUriString(urlStr, UriKind.Absolute))
                linkUrl = new Uri(urlStr);
            else return null;

            if (linkUrl.IsFile)
            {
                //  analyzedUrls.Add(url.ToString());
                //   _progressHandler.Report(new UrlReport(url, UrlStatus.SkippedFile));//_progressHandler.Report($"Url {url} skipped, because it's file!");
                return null;
            }
            if (linkUrl.IsAbsoluteUri && !_domain.IsBaseOf(linkUrl))
            {
                // analyzedUrls.Add(url.ToString());
                // _progressHandler.Report(new UrlReport(url, UrlStatus.SkippedExternal));//_progressHandler.Report($"Url {url} skipped, because it's external to domain!");
                return null;
            }
            if (!linkUrl.IsAbsoluteUri)
                linkUrl = new Uri(_domain, linkUrl);
            return linkUrl;
        }

        private bool IsUrlOnline(Uri url)
        {
            try
            {
                //Creating the HttpWebRequest
                var request = WebRequest.Create(url) as HttpWebRequest;
                //Setting the Request method HEAD, you can also use GET too.
                request.Method = "HEAD";
                //Getting the Web Response.
                var response = request.GetResponse() as HttpWebResponse;
                response.Close();

                if (response.ContentType.ToLower().Contains("html"))
                    return (int) response.StatusCode < 300;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"For url: {url} exception: {ex} occured!");
            }
            return false;
        }

        private string GetDocument(Uri urlAddress)
        {
            var request = (HttpWebRequest) WebRequest.Create(urlAddress);
            var response = (HttpWebResponse) request.GetResponse();

            //    if (response.StatusCode == HttpStatusCode.OK)
            {
                var receiveStream = response.GetResponseStream();
                StreamReader readStream;

                if (string.IsNullOrEmpty(response.CharacterSet) || string.IsNullOrWhiteSpace(response.CharacterSet))
                    readStream = new StreamReader(receiveStream);
                else
                    readStream = new StreamReader(receiveStream, Encoding.GetEncoding(response.CharacterSet));

                var data = readStream.ReadToEnd();

                response.Close();
                readStream.Close();
                return data;
            }
            _progressHandler.Report(new UrlReport(urlAddress, UrlStatus.Error));
                //_progressHandler.Report($"Couldn't access url {urlAddress}, error code: {response.StatusCode}!");
            return null;
        }
    }
}