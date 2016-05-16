using System;
using System.Windows;

namespace WebIndexer
{
    public enum UrlStatus
    {
        Processed,
        Error,
        SkippedExternal,
        SkippedFile
    }

    internal class UrlReport
    {
        public UrlReport(Uri url, UrlStatus urlStatus)
        {
            Url = url;
            Status = urlStatus;
        }

        public UrlStatus Status { get; }
        public Uri Url { get; }

        public override string ToString()
        {
            return $"{Status}: {Url}";
        }
    }

    /// <summary>
    ///     Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly WebCrawler webCrawler;

        public MainWindow()
        {
            InitializeComponent();
            webCrawler = new WebCrawler(new Progress<UrlReport>(s =>
            {
                if (s.Status == UrlStatus.Processed)
                {
                    processedOutputTextBlock.AppendText(s.Url + Environment.NewLine);
                }
                else unprocessedOutputTextBlock.AppendText(s.ToString() + Environment.NewLine);
            }));
        }

        private async void StartButton_OnClick(object sender, RoutedEventArgs e)
        {
            startButton.IsEnabled = false;
            await webCrawler.Analyze(domainTextBox.Text);
            startButton.IsEnabled = true;
            treeView.ItemsSource = webCrawler.Documents;
        }
    }
}