﻿using Lucene.Net.Analysis;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.Store;
using System;
using System.Diagnostics;
using System.IO;

namespace PerformanceCountersBrowser
{
    class Indexer : IDisposable
    {
        private readonly IndexWriter _writer;

        public static readonly Lucene.Net.Util.Version LuceneVersion = Lucene.Net.Util.Version.LUCENE_30;

        public static Analyzer CreateAnalyzer()
        {
            // create the standard analyzer. this analyzer is a compound of:
            //  - StandardTokenizer
            //		- Maximum of 255 characters per token (StandardAnalyzer.DEFAULT_MAX_TOKEN_LENGTH)
            //      - This should be a good tokenizer for most European-language documents:
            //      	- Splits words at punctuation characters, removing punctuation. However, a 
            //      	  dot that's not followed by whitespace is considered part of a token.
            //      	- Splits words at hyphens, unless there's a number in the token, in which case
            //      	- the whole token is interpreted as a product number and is not split.
            //      	- Recognizes email addresses and internet hostnames as one token.
            //  - StandardFilter
            //		- normalizes the StandardTokenizer output by:
            //			- removing 's
            //			- removing dots
            //  - LowerCaseFilter
            //  - StopFilter
            //  	- Default English stop words removal (StopAnalyzer.ENGLISH_STOP_WORDS_SET)
            //  - PorterStemFilter
            // NB: This analyzer is only used when user uses the AddDocument overload
            //     that does not receive an Analyzer type. (like we do in the AddPerformanceCounter
            //     method bellow).
            // TODO maybe we should use the SnowballAnalyzer instead.
            // TODO use an Synonym filter (SynonymFilter or SynonymAnalyzer)?
            //      (underneat it might use something like WordNetSynonymEngine).
            //      that is, convert all token to the same/first synonym to
            //      save index space.
            var analyzer = new PerformanceCounterAnalyzer(LuceneVersion);
            return analyzer;
        }

        public Indexer(string indexPath)
        {
            var directory = FSDirectory.Open(new DirectoryInfo(indexPath));
            var defaultAnalyzer = CreateAnalyzer();
            _writer = new IndexWriter(directory, defaultAnalyzer, true, IndexWriter.MaxFieldLength.UNLIMITED);
            //_writer.SetInfoStream(Console.Error); // TODO cast Console.Error into a StreamWriter...
        }

        public void AddPerformanceCounter(PerformanceCounter counter)
        {
            var doc = new Document();

            var documentTypeField = new Field("_type", "counter", Field.Store.YES, Field.Index.NOT_ANALYZED_NO_NORMS, Field.TermVector.NO)
            {
                OmitTermFreqAndPositions = true
            };
            doc.Add(documentTypeField);

            var categoryField = new Field("category", counter.CategoryName, Field.Store.YES, Field.Index.ANALYZED)
            {
                Boost = 1.5f
            };
            doc.Add(categoryField);

            var nameField = new Field("name", counter.CounterName, Field.Store.YES, Field.Index.ANALYZED)
            {
                Boost = 2
            };
            doc.Add(nameField);

            // NB: sometimes, the CounterType property getter raises an
            // InvalidOperationException:
            //
            //  The Counter layout for the Category specified is invalid, a
            //  counter of the type:  AverageCount64, AverageTimer32,
            //  CounterMultiTimer, CounterMultiTimerInverse,
            //  CounterMultiTimer100Ns, CounterMultiTimer100NsInverse,
            //  RawFraction, or SampleFraction has to be immediately
            //  followed by any of the base counter types: AverageBase,
            //  CounterMultiBase, RawBase or SampleBase.
            //
            // So, if that happens here, I'm not going to create the "type"
            // field.
            try
            {
                var typeField = new Field("type", counter.CounterType.ToString(), Field.Store.YES, Field.Index.ANALYZED_NO_NORMS, Field.TermVector.NO)
                {
                    OmitTermFreqAndPositions = true
                };
                doc.Add(typeField);
            }
            catch (Exception)
            {
                // TODO log this.
            }

            doc.Add(new Field("help", counter.CounterHelp, Field.Store.YES, Field.Index.ANALYZED));

            _writer.AddDocument(doc);
        }

        public void Dispose()
        {
            _writer.Optimize();
            _writer.Dispose();
        }
    }
}
