﻿using System;
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
                    default:
                        unprocessedOutputTextBlock.AppendText(s.ToString() + Environment.NewLine);
                        break;
                }
            }));
        }

        private async void StartButton_OnClick(object sender, RoutedEventArgs e)
        {
            startButton.IsEnabled = false;
            webCrawler.PrintShortestPaths = printShortestPathsCheckBox.IsChecked;
            await webCrawler.Analyze(domainTextBox.Text);
            startButton.IsEnabled = true;
            treeView.ItemsSource = webCrawler.Documents;
        }
    }
}