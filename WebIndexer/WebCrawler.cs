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
    /* TODO:
     * 1. https://en.wikipedia.org/wiki/Robots_exclusion_standard
     * 2. download of documents and saving on dics
     * 3.
c/ analiza grafu połączeń między dokumentami: liczba wierz. i łuków, rozkłady stopni (in, out), najkrótsze ścieżki (wszystkie pary), średnia odległość, średnica grafu, podział na klastry (współczynniki klastryzacji), odporność na ataki i awarie (zmiany grafu przy usuwaniu wierz. losowych oraz maks. stop.) (10p)
d/ wybrane 2 parametry (z obszernej literatury na ten temat), inne niz powyżej (5p)
e/ wyznacz rangi stron z zastosowaniem zaiplementowanego przez siebie iteracyjnego algorytmu PageRank (z tłumieniem i bez tłumienia), zbadaj zbieżność metody dla różnych wartości wsp. tłumienia (5p)
     */

    internal class WebCrawler
    {

        private readonly HttpClient client = new HttpClient() {Timeout = TimeSpan.FromHours(1)};
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


        private async Task<string> GetDocumentOld(Uri urlAddress)
        {
            try
            {
                HttpWebRequest request = (HttpWebRequest) WebRequest.Create(urlAddress);
                HttpWebResponse response =  (HttpWebResponse) await request.GetResponseAsync();

                if (response.StatusCode == HttpStatusCode.OK && response.ContentType.ToLower().Contains("html"))
                {
                    Stream receiveStream = response.GetResponseStream();
                    StreamReader readStream = null;

                    if (string.IsNullOrEmpty(response.CharacterSet) || string.IsNullOrWhiteSpace(response.CharacterSet))
                    {
                        readStream = new StreamReader(receiveStream);
                    }
                    else
                    {
                        readStream = new StreamReader(receiveStream, Encoding.GetEncoding(response.CharacterSet));
                    }

                    var str = await readStream.ReadToEndAsync();

                    response.Close();
                    readStream.Close();
                    return str;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"For url: {urlAddress} exception: {ex} occured!");
            }
            return null;
        }


        private string GetDocumentOld2(Uri urlAddress)
        {
            try
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(urlAddress);
                HttpWebResponse response = (HttpWebResponse) request.GetResponse();

                if (response.StatusCode == HttpStatusCode.OK && response.ContentType.ToLower().Contains("html"))
                {
                    Stream receiveStream = response.GetResponseStream();
                    StreamReader readStream = null;

                    if (string.IsNullOrEmpty(response.CharacterSet) || string.IsNullOrWhiteSpace(response.CharacterSet))
                    {
                        readStream = new StreamReader(receiveStream);
                    }
                    else
                    {
                        readStream = new StreamReader(receiveStream, Encoding.GetEncoding(response.CharacterSet));
                    }

                    var str =  readStream.ReadToEnd();

                    response.Close();
                    readStream.Close();
                    return str;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"For url: {urlAddress} exception: {ex} occured!");
            }
            return null;
        }
    }
}