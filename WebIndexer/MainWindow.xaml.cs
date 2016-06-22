using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
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
                    case ReportStatus.PLSATopicByTerms:
                        plsaTopicByTermsTextBox.AppendText(s.Message + Environment.NewLine);
                        break;
                    case ReportStatus.PLSATopicByDocument:
                        plsaTopicByDocumentTextBox.AppendText(s.Message + Environment.NewLine);
                        break;
                    case ReportStatus.PLSADocumentByTopic:
                        plsaDocumentByTopicTextBox.AppendText(s.Message + Environment.NewLine);
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

            _mp3Player.Open("Peon - praca praca.mp3");
            _mp3Player.Play(true);
            await _webCrawler.AnalyzeDocuments(int.Parse(slider_Copy1.Text),int.Parse(maxIterationsTextBox.Text),double.Parse(convergenceTextBox.Text), double.Parse(filterTextBox.Text));
            _mp3Player.Close();

            _mp3Player.Open("Work complete.mp3");
            _mp3Player.Play(false);
            await Task.Delay(2000);
            _mp3Player.Close();

            startButton.IsEnabled = true;
            startButton_Copy.IsEnabled = true;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            _webCrawler.Filter(double.Parse(filterTextBox.Text));
        }


        private readonly MP3Player _mp3Player = new MP3Player();
    }
}