using System;
using System.Net.Http;
using System.Windows;

namespace WebIndexer
{
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
                else if (s.Status == UrlStatus.Info)
                {
                    processedOutputTextBlock.AppendText(s.Message + Environment.NewLine);
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