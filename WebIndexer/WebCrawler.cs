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
using HiddenMarkov.Algorithms.PLSA.Model;
using HtmlAgilityPack;
using WebIndexer.Algorithms;
using WebIndexer.Algorithms.PLSA;
using WebIndexer.Collections;

namespace WebIndexer
{
    internal class WebCrawler
    {

        private readonly HttpClient client = new HttpClient() {Timeout = TimeSpan.FromHours(1)};

        private readonly ConcurrentDictionary<string, ConcurrentDictionary<Uri, int>> _termsByDocumentCounts =
            new ConcurrentDictionary<string, ConcurrentDictionary<Uri, int>>();

        private readonly ConcurrentDictionary<Uri, WebDocument> _documents =
            new ConcurrentDictionary<Uri, WebDocument>();

        //  private readonly DomainGraph _domainGraph = new DomainGraph();
        private readonly ConcurrentBag<Uri> _invalidUrls = new ConcurrentBag<Uri>();
        private readonly IProgress<ReportBack> _progressHandler;
        private Uri _domain;

        private readonly Regex _aHrefRegex = new Regex(@"<a\s+(?:[^>]*?\s+)?href=""([^""]*)""",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public WebCrawler(IProgress<ReportBack> progressHandler)
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

        public bool? PrintShortestPaths { get; set; }

        private Regex _metaNameNoIndex =
            new Regex(@"meta\s+name\s*=\s*""\s*robots\s*""\s*content\s*=\s*""\s*noindex\s*""\s*",
                RegexOptions.Compiled | RegexOptions.IgnoreCase);


        private ProbabilisticLSA plsa;
        public async Task AnalyzeDocuments(int numberOfTopics, int maxIterations, double convergence, double filter)
        {
            var termsByDocumentMatrix = new MatrixHashTable<Term,Uri,int>(_termsByDocumentCounts.Keys.Select(s => new Term(s)).ToArray(),
                _termsByDocumentCounts.Values.Select( v => v.Keys).SelectMany((e1) => e1).Distinct().ToArray());

            foreach (var term in termsByDocumentMatrix.Key1Space)
                foreach (var uri in termsByDocumentMatrix.Key2Space)
                    if (_termsByDocumentCounts.ContainsKey(term.word) &&
                        _termsByDocumentCounts[term.word].ContainsKey(uri))
                        termsByDocumentMatrix[term, uri] = _termsByDocumentCounts[term.word][uri];
                    else
                        termsByDocumentMatrix[term, uri] = 0;


            plsa = new ProbabilisticLSA(termsByDocumentMatrix,numberOfTopics) {Convergence = convergence,MaximumIterations = maxIterations};

            await Task.Run(() => plsa.DoWork());
            

            Filter(filter);

            _progressHandler.Report(new ReportBack("TermsByTopicMatrix:", ReportStatus.PLSATermsByTopic));

            foreach (var term in plsa.TermsByTopicMatrix.Key1Space)
                foreach (var topic in plsa.TermsByTopicMatrix.Key2Space)
                    _progressHandler.Report(
                        new ReportBack($@"[{term.word},{topic.name}]={plsa.TermsByTopicMatrix[term, topic]}",
                            ReportStatus.PLSATermsByTopic));

            _progressHandler.Report(new ReportBack("TopicByDocumentMatrix:", ReportStatus.PLSATopicByDocument));

            foreach (var topic in plsa.TopicByDocumentMatrix.Key1Space)
                foreach (var url in plsa.TopicByDocumentMatrix.Key2Space)
                    _progressHandler.Report(
                        new ReportBack($@"[{topic.name},{url}]={plsa.TopicByDocumentMatrix[topic, url]}",
                            ReportStatus.PLSATopicByDocument));


            _progressHandler.Report(new ReportBack("TopicByTermsMatrix:", ReportStatus.PLSATopicByTerms));

            foreach (var topic in plsa.TermsByTopicMatrix.Key2Space)
                foreach (var term in plsa.TermsByTopicMatrix.Key1Space)
                    _progressHandler.Report(
                        new ReportBack($@"[{term.word},{topic.name}]={plsa.TermsByTopicMatrix[term, topic]}",
                            ReportStatus.PLSATopicByTerms));

            _progressHandler.Report(new ReportBack("DocumentByTopicMatrix:", ReportStatus.PLSADocumentByTopic));

            foreach (var url in plsa.TopicByDocumentMatrix.Key2Space)
                foreach (var topic in plsa.TopicByDocumentMatrix.Key1Space)
                    _progressHandler.Report(
                        new ReportBack($@"[{topic.name},{url}]={plsa.TopicByDocumentMatrix[topic, url]}",
                            ReportStatus.PLSADocumentByTopic));
        }

        public void Filter(double filter)
        {
            _progressHandler.Report(
                new ReportBack(
                    $@"PLSA report (Convergence={plsa.Convergence}, MaximumIterations={plsa.MaximumIterations}, numberOfTopics={plsa
                        .TopicByDocumentMatrix.Key1Space.Count}, filter={filter})", ReportStatus.ProbabilisticLSA));

            foreach (var topic1 in plsa.TopicByDocumentMatrix.Key1Space)
            {
                _progressHandler.Report(
                    new ReportBack(
                        $@"{Environment.NewLine}---------------------------------------------------{Environment.NewLine}{topic1}",
                        ReportStatus.ProbabilisticLSA));

                foreach (var uri in plsa.TopicByDocumentMatrix.Key2Space)
                {
                    var prob = double.MinValue;
                    foreach (var topic2 in plsa.TopicByDocumentMatrix.Key1Space)
                    {
                        if (plsa.TopicByDocumentMatrix[topic2, uri] > prob)
                            prob = plsa.TopicByDocumentMatrix[topic2, uri];
                    }
                    if (plsa.TopicByDocumentMatrix[topic1, uri] == prob)
                    {
                        if (prob >= (1.0/plsa.TopicByDocumentMatrix.Key1Space.Count)*filter)
                            _progressHandler.Report(new ReportBack($@"{uri}", ReportStatus.ProbabilisticLSA));
                    }
                }
            }
        }

        public async Task Analyze(string domain)
        {
            SetThreading();


            _domain = new Uri(domain);



            SetDomainDirectory();

            await AnalyzeRobotsTxt();
            // _domain = GetValidUrlOrNull(domain);
            //if (_domain == null)
            //  return Task.Delay(1);
            _documents.Clear();
           

          //  if (!_disallowedUrls.Contains(_domain))
          //  {
                var stw = Stopwatch.StartNew();
            _documents[_domain] = new WebDocument() {AbsoluteUrl = _domain};

                await AnalyzeUrl(_domain,null);
            ClearGraph();
                stw.Stop();            _progressHandler.Report(new ReportBack($"Pages count: {_documents.Count}{Environment.NewLine}Time: {stw.Elapsed.TotalMilliseconds}ms{Environment.NewLine}Speed: {_documents.Count/stw.Elapsed.TotalSeconds} pages/second"));

            var stw2 = Stopwatch.StartNew();
            
            await AnalyzeGraph();
            
            stw2.Stop();
            _progressHandler.Report(new ReportBack($"Graph analysis took {stw2.ElapsedMilliseconds}ms"));

            // }else
            //  await Task.Delay(1);
        }

        private void SetThreading()
        {
            int minIO, minThreads;
            ThreadPool.GetMinThreads(out minThreads, out minIO);
            ThreadPool.SetMinThreads(1, minIO);

            int maxThreads, maxThreadsAsync;
            ThreadPool.GetMaxThreads(out maxThreads, out maxThreadsAsync);

            Debug.WriteLine($"Max threads:{maxThreads}, max async I/O:{maxThreadsAsync}");
            ThreadPool.SetMaxThreads(MaxThreads, maxThreadsAsync);

            ThreadPool.GetMaxThreads(out maxThreads, out maxThreadsAsync);
            Debug.WriteLine($"Max threads:{maxThreads}, max async I/O:{maxThreadsAsync}");


            options = new ParallelOptions() { MaxDegreeOfParallelism = MaxConcurrency };
        }

        public int MaxConcurrency { get; set; } = 8;
        private void ClearGraph()
        {
            foreach (var webDocument in _documents)
            {
                
                if (!webDocument.Value.Analyzed || _invalidUrls.Contains(webDocument.Key))
                {
                    WebDocument toRemove;
                    _documents.TryRemove(webDocument.Key, out toRemove);

                    if(!_invalidUrls.Contains(webDocument.Key))
                        _invalidUrls.Add(toRemove.AbsoluteUrl);
                }
            }

            foreach (var webDocument in _documents.Values)
            {
                foreach (var invalidUrl in _invalidUrls)
                {
                    if (webDocument.OutLinks.Contains(invalidUrl))
                    {
                        Uri toRemove;
                        webDocument.OutLinks.TryTake(out toRemove);
                    }

                    if (webDocument.InLinks.Contains(invalidUrl))
                    {
                        Uri toRemove;
                        webDocument.InLinks.TryTake(out toRemove);
                    }
                }
            }
        }

        private void SetDomainDirectory()
        {
            var illegalChars = Path.GetInvalidPathChars();
            var illegalChars2 = Path.GetInvalidFileNameChars();
            var windowsIllegalChars = @"\/:".ToCharArray();
            _domainDirectory =
                new string(
                    _domain.ToString().Where(c => !illegalChars.Contains(c) && !windowsIllegalChars.Contains(c)).ToArray());
        }

        private async Task AnalyzeGraph()
        {     
            _progressHandler.Report(new ReportBack($"Vertices count: {_documents.Count}"));//liczba wierz.

   
            var inEdgesCount = _documents.Values.Aggregate(0, (c, d) => c += d.Indegree);
            var outEdgesCount = _documents.Values.Aggregate(0, (c, d) => c += d.Outdegree);


            //if(inEdgesCount != outEdgesCount)
            //throw new Exception();

            _progressHandler.Report(new ReportBack($"Arrows count: {inEdgesCount}"));//liczba łuków



            _progressHandler.Report(new ReportBack($"Average indegree: {1.0*inEdgesCount/_documents.Count}"));
            _progressHandler.Report(new ReportBack($"Average outdegree: {1.0 * outEdgesCount / _documents.Count}"));

            //  _progressHandler.Report(new ReportBack($"Average indegree: {inEdgesCount / _documents.Count}"));


            //rozkłady stopni (in, out)
            InOutDistribution();

            var pageRank = new PageRank(_documents);
            pageRank.DoWork();

            var pagerankSum = _documents.Values.Aggregate(0.0, (s, d) => s += d.PageRank);
            _progressHandler.Report(new ReportBack($"Average pagerank: {pagerankSum / _documents.Count}"));
            SavePageRank();


            var floydWarshall = new FloydWarshall(_documents);
            await floydWarshall.DoWorkAsync();

            _progressHandler.Report(new ReportBack($"Average distance: {floydWarshall.GetAverageDistance()}"));//średnia odległość

            _progressHandler.Report(new ReportBack($"Diameter: {floydWarshall.GetDiameter()}"));//średnica grafu
            _progressHandler.Report(new ReportBack($"Radius: {floydWarshall.GetRadius()}"));
            if (PrintShortestPaths == true) //najkrótsze ścieżki (wszystkie pary)
            {
                await Task.Run(() =>
                {

                        //var str = await Task.Run(() => floydWarshall.BuildPaths());//floydWarshall.BuildPathsAsync();//Task.Run(()=> BuildPaths(floydWarshall));
                        var str = floydWarshall.BuildPaths();
                        _progressHandler.Report(new ReportBack(str, ReportStatus.Information));
                    File.WriteAllText("paths.txt",str);
                
                });
            }
        }

        private void SavePageRank()
        {
            var distribution = _documents.Values.ToList();

            distribution.Sort((d1, d2) => d2.PageRank.CompareTo(d1.PageRank));

            var pagerankFile = new StreamWriter("pagerank.txt");
            foreach (var document in distribution)
            {
                pagerankFile.WriteLine($"{document.AbsoluteUrl} {document.PageRank}");
            }
            pagerankFile.Close();
        }

        private void InOutDistribution()
        {
            var distribution = _documents.Values.ToList();
            InDistribution(distribution);
            OutDistribution(distribution);

        }

        private static void InDistribution(List<WebDocument> distribution)
        {
            distribution.Sort((d1, d2) => d2.Indegree.CompareTo(d1.Indegree));

            var inDistributionFile = new StreamWriter("inDistribution.txt");
            foreach (var document in distribution)
            {
                inDistributionFile.WriteLine($"{document.AbsoluteUrl} {document.Indegree}");
            }
            inDistributionFile.Close();
        }

        private static void OutDistribution(List<WebDocument> distribution)
        {
            distribution.Sort((d1, d2) => d2.Outdegree.CompareTo(d1.Outdegree));

            var outDistributionFile = new StreamWriter("outDistribution.txt");
            foreach (var document in distribution)
            {
                outDistributionFile.WriteLine($"{document.AbsoluteUrl} {document.Outdegree}");
            }
            outDistributionFile.Close();
        }

        private readonly object _lockObject = new object();

        private async Task AnalyzeRobotsTxt()
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
                                : new Uri(_domain, new Uri(match.Groups[1].Value,UriKind.Relative)));
                        }
                        else
                            break;
                    }

            _disallowedUrls = disallowedUrlsList.ToArray();
        }

        private readonly Regex _userAgentRegex = new Regex(@"User-agent\s*:\s*\*", RegexOptions.Compiled|RegexOptions.IgnoreCase);
        private readonly Regex _disallowRegex = new Regex(@"Disallow\s*:\s*(.+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public int MaxThreads { get; set; } = 20;

        ParallelOptions options;
        private Uri[] _disallowedUrls;

        private async Task AnalyzeUrl(Uri url, Uri sourceUrl)
        {
           // if(_disallowedUrls.Any(u => u.IsBaseOf(url)))
             //   return;
            _documents[url].Analyzed = true;


            //1. download document and measure time
            var stopwatch = Stopwatch.StartNew();//new Stopwatch();
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
            //2.5. analyze document for PLSA
            AnalyzeDocument(url, str);

            var tasks = new ConcurrentBag<Task>();

            var matches = _aHrefRegex.Matches(str).Cast<Match>();
            //3. analyze document
            //foreach (Match match in matches)      
            Parallel.ForEach(matches, options,  match =>
            {
                var linkUrl = GetValidUrlOrNull(match.Groups[1].Value);

                if (linkUrl == null || _invalidUrls.Contains(linkUrl)|| _disallowedUrls?.Any(u => u.IsBaseOf(linkUrl))==true)
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

            _progressHandler.Report(new ReportBack(url, ReportStatus.UrlProcessed)); //_progressHandler.Report($"{url} - OK!");
        }

        private void AnalyzeDocument(Uri url, string html)
        {
            var plainText = _htmlToTextConverter.ConvertHtml(html);

            var words = plainText.ToLowerInvariant().Split(new[] {' ','.',',','"',';','\'','\n','\r',':','=','-','+','/','(',')','[',']','{','}','#','^','@','!','?','>','<','&','%','|'}, StringSplitOptions.RemoveEmptyEntries);

            foreach (var word in words)
            {
                // ReSharper disable once InconsistentlySynchronizedField
                if(!_termsByDocumentCounts.ContainsKey(word))
                    // ReSharper disable once InconsistentlySynchronizedField
                    _termsByDocumentCounts.TryAdd(word, new ConcurrentDictionary<Uri, int>());
                // ReSharper disable once InconsistentlySynchronizedField
                if (!_termsByDocumentCounts[word].ContainsKey(url))
                {
                    // ReSharper disable once InconsistentlySynchronizedField
                    _termsByDocumentCounts[word].TryAdd(url, 1);
                }
                else
                {
                    //Interlocked.Increment(ref _termsByDocumentCounts[word][url]);
                    lock (_lockObject)
                    {
                        _termsByDocumentCounts[word][url]++;
                    }
                }
            }

        }

        private string _domainDirectory;
        private readonly HtmlToText _htmlToTextConverter= new HtmlToText();


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