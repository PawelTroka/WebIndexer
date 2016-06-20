using System;
using System.Collections.Generic;
using System.Linq;
using HiddenMarkov.Algorithms.PLSA.Model;
using WebIndexer.Collections;

namespace WebIndexer.Algorithms.PLSA
{
    // ReSharper disable once InconsistentNaming
    class ProbabilisticLSA
    {
        //input fields
        private readonly MatrixHashTable<Term, Uri, int> _termsByDocumentMatrix;
        private readonly IReadOnlyCollection<Uri> _documents;
        private readonly IReadOnlyCollection<Term> _terms;
        private readonly IReadOnlyCollection<Topic> _topics;


        //output properties
        public MatrixHashTable<Term, Topic, double> TermsByTopicMatrix { get; private set; }
        public MatrixHashTable<Topic, Uri, double> TopicByDocumentMatrix { get; private set; }

        public double Convergence { get; set; } = 0.00001;
        public int MaximumIterations { get; set; } = 1000;

        public ProbabilisticLSA(MatrixHashTable<Term, Uri, int> termsByDocumentMatrix, int numberOfTopics)
        {
            this._termsByDocumentMatrix = termsByDocumentMatrix;
            this._terms = termsByDocumentMatrix.Key1Space;
            this._documents = termsByDocumentMatrix.Key2Space;

            this._topics = Enumerable.Range(1,numberOfTopics).Select(n => new Topic("Topic "+n)).ToArray();

            InitData();
        }

        private void InitData()
        {
            TermsByTopicMatrix = new MatrixHashTable<Term, Topic, double>(_terms, _topics);
            TopicByDocumentMatrix = new MatrixHashTable<Topic, Uri, double>(_topics, _documents);
            var randomGenerator = new Random();
            var normalizingFactor = 0.0;
            foreach (var term in _terms)
            {
                foreach (var topic in _topics)
                {
                    TermsByTopicMatrix[term, topic] = randomGenerator.NextDouble();
                    normalizingFactor += TermsByTopicMatrix[term, topic];
                }
                // var normalizingFactor = TermsByTopicMatrix.Aggregate();


                foreach (var topic in _topics)
                {
                    TermsByTopicMatrix[term, topic] = TermsByTopicMatrix[term, topic] / normalizingFactor;
                }
                normalizingFactor = 0.0;
            }

            foreach (var topic in _topics)
            {
                foreach (var document in _documents)
                {
                    TopicByDocumentMatrix[topic, document] = randomGenerator.NextDouble();
                    normalizingFactor += TopicByDocumentMatrix[topic, document];
                }
                // var normalizingFactor = TermsByTopicMatrix.Aggregate();


                foreach (var document in _documents)
                {
                    TopicByDocumentMatrix[topic, document] = TopicByDocumentMatrix[topic, document] / normalizingFactor;
                }
                normalizingFactor = 0.0;
            }
        }

        public void DoWork()
        {
            for (int i = 0; i < MaximumIterations; i++)
            {
                DoCalculate();
            }
        }

        private void DoCalculateTermsByTopic()
        {
            foreach (var topic in _topics)
            {
                var normalizingFactor = 0.0;
                foreach (var term in _terms)
                {
                    var tmp = 0.0;
                    foreach (var doc in _documents)
                    {
                        var denominator = 0.0;
                        var enumerator = 0.0;
                        foreach (var top in _topics)
                        {
                            denominator += TermsByTopicMatrix[term, top] * TopicByDocumentMatrix[top, doc];
                        }
                        enumerator = _termsByDocumentMatrix[term, doc] * TopicByDocumentMatrix[topic, doc];
                        tmp += enumerator / denominator;
                    }
                    TermsByTopicMatrix[term, topic] = TermsByTopicMatrix[term, topic] * tmp;
                    normalizingFactor += TermsByTopicMatrix[term, topic];
                    //TermsByTopicMatrix
                }
                //normalization
                foreach (var term in _terms)
                {
                    TermsByTopicMatrix[term, topic] = TermsByTopicMatrix[term, topic] / normalizingFactor;
                }
            }

        }
        private void DoCalculateTopicByDocument()
        {
            foreach (var topic in _topics)
            {
                var normalizingFactor = 0.0;
                foreach (var doc in _documents)
                {
                    var tmp = 0.0;
                    foreach (var term in _terms)
                    {
                        var denominator = 0.0;
                        foreach (var top in _topics)
                        {
                            denominator += TermsByTopicMatrix[term, top] * TopicByDocumentMatrix[top, doc];
                        }
                        var enumerator = _termsByDocumentMatrix[term, doc] * TermsByTopicMatrix[term, topic];
                        tmp += enumerator / denominator;
                    }
                    TopicByDocumentMatrix[topic, doc] = TopicByDocumentMatrix[topic, doc] * tmp;
                    normalizingFactor += TopicByDocumentMatrix[topic, doc];
                    //TermsByTopicMatrix
                }
                //normalization
                foreach (var doc in _documents)
                {
                    TopicByDocumentMatrix[topic, doc] = TopicByDocumentMatrix[topic, doc] / normalizingFactor;
                }
            }

        }

        private void DoCalculate()
        {
            DoCalculateTermsByTopic();
            DoCalculateTopicByDocument();
        }
    }
}
