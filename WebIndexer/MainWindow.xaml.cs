using System;
using System.Net.Http;
using System.Threading;
using System.Windows;

namespace WebIndexer
{
    /// <summary>
    ///     Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly WebCrawler _webCrawler;
        public MainWindow()
        {

            InitializeComponent();
            startButton_Copy.IsEnabled = false;
            _webCrawler = new WebCrawler(new Progress<ReportBack>(s =>
            {
                switch (s.Status)
                {
                    case ReportStatus.UrlProcessed:
                        processedOutputTextBlock.AppendText(s.Url + Environment.NewLine);
                        break;
                    case ReportStatus.Information:
                        processedOutputTextBlock.AppendText(s.Message + Environment.NewLine);
                        break;
                    case ReportStatus.ShortestPaths:
                        shortestPathsTextBox.AppendText(s.Message + Environment.NewLine);
                        break;
                    case ReportStatus.ProbabilisticLSA:
                        plsaTextBox.AppendText(s.Message + Environment.NewLine);
                        break;

                    case ReportStatus.PLSATermsByTopic:
                        plsaTermsByTopicTextBox.AppendText(s.Message + Environment.NewLine);
                        break;

                    case ReportStatus.PLSATopicByDocument:
                        plsaTopicByDocumentTextBox.AppendText(s.Message + Environment.NewLine);
                        break;

                    default:
                        unprocessedOutputTextBlock.AppendText(s.ToString() + Environment.NewLine);
                        break;
                }
            }));
        }

        private async void StartButton_OnClick(object sender, RoutedEventArgs e)
        {
            startButton.IsEnabled = false;
            _webCrawler.PrintShortestPaths = printShortestPathsCheckBox.IsChecked;
            _webCrawler.MaxThreads = int.Parse(slider.Text);
            _webCrawler.MaxConcurrency = int.Parse(slider_Copy.Text);
            await _webCrawler.Analyze(domainTextBox.Text);
            startButton.IsEnabled = true;
            startButton_Copy.IsEnabled = true;
            treeView.ItemsSource = _webCrawler.Documents;
        }

        private async void StartButton_Copy_OnClick(object sender, RoutedEventArgs e)
        {
            startButton.IsEnabled = false;
            startButton_Copy.IsEnabled = false;

            await _webCrawler.AnalyzeDocuments(int.Parse(slider_Copy1.Text),int.Parse(maxIterationsTextBox.Text),double.Parse(convergenceTextBox.Text));

            startButton.IsEnabled = true;
            startButton_Copy.IsEnabled = true;
        }
    }
}