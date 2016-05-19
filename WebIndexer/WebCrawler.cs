using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace WebIndexer
{
    internal class WebCrawler
    {

        private readonly HttpClient client = new HttpClient();
        private readonly ConcurrentDictionary<Uri, WebDocument> _documents =
            new ConcurrentDictionary<Uri, WebDocument>();

      //  private readonly DomainGraph _domainGraph = new DomainGraph();
        private readonly ConcurrentBag<Uri> _invalidUrls = new ConcurrentBag<Uri>();
        private readonly IProgress<UrlReport> _progressHandler;
        private Uri _domain;

        private readonly Regex aHrefRegex = new Regex(@"<a\s+(?:[^>]*?\s+)?href=""([^""]*)""", RegexOptions.Compiled);

        public WebCrawler(IProgress<UrlReport> progressHandler)
        {
            _progressHandler = progressHandler;
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/html"));
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


        public async Task Analyze(string domain)
        {
            _domain = new Uri(domain);
            // _domain = GetValidUrlOrNull(domain);
            //if (_domain == null)
            //  return Task.Delay(1);
            _documents[_domain] = new WebDocument() {AbsoluteUrl = _domain};
                var stw = Stopwatch.StartNew();
                await AnalyzeUrl(_domain,null);
            foreach (var webDocument in _documents)
            {
                WebDocument toRemove;
                if (!webDocument.Value.Analyzed)
                    _documents.TryRemove(webDocument.Key,out toRemove);
            }
                stw.Stop();
                _progressHandler.Report(new UrlReport($"Time: {stw.Elapsed.TotalMilliseconds}ms"));

        }


        private async Task AnalyzeUrl(Uri url, Uri sourceUrl)
        {
            
            _documents[url].Analyzed = true;
            var stopwatch = new Stopwatch();

            //1. download document and measure time
            stopwatch.Start();
            var str = await GetDocument(url);
            stopwatch.Stop();

            if (str == null)
            {
                _invalidUrls.Add(url);
                _documents[url].Analyzed = false;
                return;
            }
            _documents[url].DownloadTime = stopwatch.Elapsed.TotalMilliseconds;

            if (sourceUrl != null)
            {
                _documents[sourceUrl].OutLinks.Add(url);
                _documents[url].InLinks.Add(sourceUrl);
            }
            //2. save document
            //TODO: save

            var tasks = new ConcurrentBag<Task>();

            var matches = aHrefRegex.Matches(str).Cast<Match>();
            //3. analyze document
            //foreach (Match match in matches)      
            Parallel.ForEach(matches,  match =>
            {
                var linkUrl = GetValidUrlOrNull(match.Groups[1].Value);

                if (linkUrl == null || _invalidUrls.Contains(linkUrl))
                    //continue;
                    return;

             //   _domainGraph[url, linkUrl]++;
                
                if (!_documents.ContainsKey(linkUrl))
                    _documents.TryAdd(linkUrl, new WebDocument() {AbsoluteUrl = linkUrl});

                //4. start analyze for all nested links
                if (!_documents[linkUrl].Analyzed)
                    tasks.Add(AnalyzeUrl(linkUrl, url)); //await AnalyzeUrl(linkUrl);
                else
                {
                    _documents[url].OutLinks.Add(linkUrl);
                    _documents[linkUrl].InLinks.Add(url);
                }
            });

            await Task.WhenAll(tasks);

            _progressHandler.Report(new UrlReport(url, UrlStatus.Processed)); //_progressHandler.Report($"{url} - OK!");
        }


        private Uri GetValidUrlOrNull(string urlStr)
        {
            if (urlStr.Contains('#'))
                urlStr = urlStr.Remove(urlStr.IndexOf('#'));

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


        private async Task<string> GetDocument(Uri urlAddress)
        {
            string str=null;
            try
            {
                using (HttpResponseMessage response = await client.GetAsync(urlAddress))
                {
                    if (response.IsSuccessStatusCode) 
                    using (HttpContent content = response.Content)
                    {
                     //   Debug.WriteLine(content.Headers.ContentType.MediaType);
                    if (content.Headers.ContentType.MediaType.ToLower().Contains("html"))
                        str = await content.ReadAsStringAsync();
                 //  // else
                  //  {
                  //      str = null;
                 //   }
                }
               }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"For url: {urlAddress} exception: {ex} occured!");
            }

            return str;
        }
    }
}