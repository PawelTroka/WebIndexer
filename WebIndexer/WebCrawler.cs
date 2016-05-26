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
using HtmlAgilityPack;

namespace WebIndexer
{
    /* TODO:
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

        private Regex metaNameNoIndex = new Regex(@"meta\s+name\s*=\s*""\s*robots\s*""\s*content\s*=\s*""\s*noindex\s*""\s*",RegexOptions.Compiled|RegexOptions.IgnoreCase);

        public async Task Analyze(string domain)
        {
            _domain = new Uri(domain);

            var illegalChars = Path.GetInvalidPathChars();
            var windowsIllegalChars = @"\/:".ToCharArray();
            _domainDirectory = new string(_domain.ToString().Where(c => !illegalChars.Contains(c) && !windowsIllegalChars.Contains(c)).ToArray());

            AnalyzeRobotsTxt();
            // _domain = GetValidUrlOrNull(domain);
            //if (_domain == null)
            //  return Task.Delay(1);
            _documents.Clear();
           

          //  if (!_disallowedUrls.Contains(_domain))
          //  {
                var stw = Stopwatch.StartNew();_documents[_domain] = new WebDocument() {AbsoluteUrl = _domain};

                await AnalyzeUrl(_domain,null);
            foreach (var webDocument in _documents)
            {
                WebDocument toRemove;
                if (!webDocument.Value.Analyzed)
                    _documents.TryRemove(webDocument.Key,out toRemove);
            }
                stw.Stop();            _progressHandler.Report(new UrlReport($"Pages count: {_documents.Count}{Environment.NewLine}Time: {stw.Elapsed.TotalMilliseconds}ms{Environment.NewLine}Speed: {_documents.Count/stw.Elapsed.TotalSeconds} pages/second"));
  
           // }else
          //  await Task.Delay(1);
        }

        private async void AnalyzeRobotsTxt()
        {
            var robotsUri = new Uri(_domain,"/robots.txt");
            var robotsTxt = await GetDocument(robotsUri);

            if (robotsTxt == null) return;

            var disallowedUrlsList = new List<Uri>();

            var lines = robotsTxt.Split('\n');

            for (int i = 0; i < lines.Length; i++)
                if (_userAgentRegex.IsMatch(lines[i]))
                    for (i=i+1; i < lines.Length; i++)
                    {
                        var match = _disallowRegex.Match(lines[i]);

                        if (match.Success)
                        {
                            disallowedUrlsList.Add(match.Groups[1].Value == "/"
                                ? _domain
                                : new Uri(_domain, new Uri(match.Groups[1].Value)));
                        }
                        else
                            break;
                    }

            _disallowedUrls = disallowedUrlsList.ToArray();
        }

        private readonly Regex _userAgentRegex = new Regex(@"User-agent\s*:\s*\*", RegexOptions.Compiled|RegexOptions.IgnoreCase);
        private readonly Regex _disallowRegex = new Regex(@"Disallow\s*:\s*(.+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);


        private Uri[] _disallowedUrls;

        private async Task AnalyzeUrl(Uri url, Uri sourceUrl)
        {
           // if(_disallowedUrls.Any(u => u.IsBaseOf(url)))
             //   return;
            _documents[url].Analyzed = true;
            var stopwatch = Stopwatch.StartNew();//new Stopwatch();

            //1. download document and measure time
            stopwatch.Start();
            var str = await GetDocument(url);//GetDocument3(url);//await GetDocument(url);
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
            SaveDocument(url,str);

            var tasks = new ConcurrentBag<Task>();

            var matches = aHrefRegex.Matches(str).Cast<Match>();
            //3. analyze document
            //foreach (Match match in matches)      
            Parallel.ForEach(matches,  match =>
            {
                var linkUrl = GetValidUrlOrNull(match.Groups[1].Value);

                if (linkUrl == null || _invalidUrls.Contains(linkUrl)|| _disallowedUrls.Any(u => u.IsBaseOf(linkUrl)))
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

        private string _domainDirectory;



        private void SaveDocument(Uri uri,string content)
        {
            var filename = _domain.MakeRelativeUri(uri).ToString();

            var index = filename.LastIndexOfAny(@"\/".ToCharArray());

            var lastPart = (index >= 0) ? filename.Substring(index) : filename;

            if (!lastPart.Contains('.'))
            {
                if (filename.Length > 0 && filename.Last() != '/')
                    filename += '/';
                filename += "index.html";
            }

            filename = Path.Combine(_domainDirectory, filename);
            System.IO.FileInfo file = new System.IO.FileInfo(filename);
            file.Directory.Create(); // If the directory already exists, this method does nothing.
            System.IO.File.WriteAllText(file.FullName, content);
        }


        private Uri GetValidUrlOrNull(string urlStr)
        {
            if (urlStr.Contains('#'))
                urlStr = urlStr.Remove(urlStr.IndexOf('#'));
            if (urlStr.Contains('?'))
                urlStr = urlStr.Remove(urlStr.IndexOf('?'));

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
                            if (content.Headers.ContentType.MediaType.ToLower().Contains("html")|| content.Headers.ContentType.MediaType.ToLower().Contains("text"))
                                str = await content.ReadAsStringAsync();
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

        private readonly HtmlWeb web = new HtmlWeb();
        private string GetDocument3(Uri urlAddress)
        {
            try
            {
                var document = web.Load(urlAddress.ToString());
                if(document?.DocumentNode?.InnerHtml != null)
                return document.DocumentNode.InnerHtml;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"For url: {urlAddress} exception: {ex} occured!");
            }
            return null;
        }
    }
}