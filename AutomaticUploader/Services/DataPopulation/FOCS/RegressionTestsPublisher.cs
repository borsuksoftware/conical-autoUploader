using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Options;

namespace BorsukSoftware.Conical.AutomaticUploader.Services.DataPopulation.FOCS
{
    /// <summary>
    /// Background service to populate a Conical instance with integration test data
    /// </summary>
    /// <remarks>
    /// In this example, we've created some fake objects for comparison and associated comparators to show that users can
    /// use a range of different comparison functions within notionally the same analysis purpose. Being explicit, this means
    /// that a user could use a different set of comparison functions for 2-D vol surfaces vs. a yield curve etc.
    /// 
    /// <para>In general, the assumption for the regresison tests is that they compare their objects against a previous version
    /// which is stored on disc (as opposed to being sourced from a live instance on the fly). This leads to it being desirable
    /// to store the updated candidate object as an additional file in case of there being a difference found; this updated file
    /// can then be used to replace the expected results in the case that people are comfortable with the new version being 
    /// correct.</para>
    /// 
    /// <para>Note that the responsibility of updating the expected source is external to Conical, but it would be expected that
    /// the CI process, after approvals etc., would be able to trigger an executable which could connect to Conical to download
    /// the new expected results (typically using a 'by convention' approach to naming by having a redirection file 'updatedResults.json'
    /// which would contain information as to what source files should be replaced at what location).</para>
    /// </remarks>
    public class RegressionTestsPublisher : Microsoft.Extensions.Hosting.BackgroundService
    {
        private readonly IOptions<ServerOptions> _serverOptions;
        private readonly TelegramService _telegramService;
        private readonly IOptions<Services.RequestedLifetimeServiceOptions> _requestedLifetimeServiceOptions;
        private readonly Services.RequestedLifetimeService _requestedLifetimeService;
        private readonly IOptions<FOCSUploadSettings> _focsUploadSettings;
        private readonly ProductCreationService _productCreationService;
        private readonly Client.IMemorySnapshot _startMemorySnapshot;

        public RegressionTestsPublisher(IOptions<ServerOptions> serverOptions,
            IOptions<FOCSUploadSettings> focsUploadSettings,
            IOptions<Services.RequestedLifetimeServiceOptions> requestedLifetimeServiceOptions,
            Services.RequestedLifetimeService requestedLifetimeService,
            ProductCreationService productCreationService,
            TelegramService telegramService)
        {
            _serverOptions = serverOptions;
            _focsUploadSettings = focsUploadSettings;
            _telegramService = telegramService;

            _requestedLifetimeService = requestedLifetimeService;
            _requestedLifetimeServiceOptions = requestedLifetimeServiceOptions;

            _productCreationService = productCreationService;

            _startMemorySnapshot = Client.MemorySnapshot.SnapshotProcess();
        }

        protected async override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var url = _serverOptions.Value.Url;
            var httpClient = new System.Net.Http.HttpClient();
            httpClient.BaseAddress = new Uri(url);
            if (!string.IsNullOrEmpty(_serverOptions.Value.AccessToken))
                httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_serverOptions.Value.AccessToken}");
            var restApiService = new Client.REST.ApiService(httpClient);
            var server = new Client.REST.AccessLayer(restApiService);

            while (true)
            {
                try
                {
                    string productName = _focsUploadSettings.Value.ProductName;
                    var focs = await _productCreationService.EnsureProductExists(server);

                    var endDate = DateTime.Today;
                    var startDate = endDate.AddDays(_focsUploadSettings.Value.RegressionTestsStartDateOffset);

                    if (DateTime.UtcNow.TimeOfDay.TotalHours < _focsUploadSettings.Value.RegressionTestsLaunchTimeHours)
                        endDate = endDate.AddDays(-1);

                    var searchResults = await server.SearchTestRunSets(new[] { productName }, new[] { Client.TestRunSetStatus.Standard }, null, null, null, startDate, endDate.AddDays(1), null, null, new[] { "regressiontests" });
                    var existingDates = searchResults.Results.Where(trs => trs.Tags.Contains("regressiontests")).Select(trs => trs.RefDate).Distinct().ToHashSet();

                    for (DateTime refDate = startDate; refDate <= endDate; refDate = refDate.AddDays(1))
                    {
                        // Check to see if we already have regression test data
                        if (existingDates.Contains(refDate))
                            continue;

                        // We don't test on the weekend
                        if (refDate.DayOfWeek == DayOfWeek.Saturday || refDate.DayOfWeek == DayOfWeek.Sunday)
                            continue;

                        await _telegramService.SendMessage($"FOCS - Publishing regression test results for {refDate:dd-MM-yyyy}");


                        var trs = await focs.CreateTestRunSet("Regression Tests", "Low level regression tests", refDate, DateTime.UtcNow, new[] { "regressiontests", "ci" });

                        await PublishAnalysisResults(trs);
                        await PublishMarketTests(trs);
                        await PublishRiskTests(trs);
                        // await PublishStaticTests(trs);

                        await trs.SetStatus(Client.TestRunSetStatus.Standard);
                    }

                    // Wait for a minute before seeing if there's anything else left to do
                    await Task.Delay(60000);
                }
                catch (Exception ex)
                {
                    await _telegramService.SendMessage($"FOCS Regression Tests Publisher - Exception caught - {ex}");
                }

                if( _requestedLifetimeServiceOptions.Value.Mode == RequestedLifetimeMode.OneOff)
                {
                    Console.WriteLine("FOCS - Regression - Single operation mode - registering completion");
                    _requestedLifetimeService.RegisterPopulationFinish();
                    return;
                }
            }
        }

        /// <summary>
        /// Create some arbitrary results for analysis jobs
        /// </summary>
        /// <remarks>Analysis jobs are based off having results text</remarks>
        /// <param name="testRunSet"></param>
        /// <returns></returns>
        private async Task PublishAnalysisResults(Client.ITestRunSet testRunSet)
        {
            var assetClasses = new[]
            {
                "Commodities",
                "FX",
                "Credit",
                "Equities",
                "Inflation",
                "Rates"
            };

            var random = new Random();
            foreach (var assetClass in assetClasses)
            {
                var testRun = await testRunSet.CreateTestRun($"Analysis\\{assetClass}\\General", "Analysis example", "Analysis", Client.TestRunStatus.Passed);

                var lines = Enumerable.Range(0, random.Next(400) + 18).
                    Select(idx => $"This is sample output line #{idx}");

                string text = string.Join("\n", lines);
                await testRun.PublishTestRunResultsText(text);

                await FOCSUtils.PublishTestRunAssemblies(testRun);
                await FOCSUtils.PublishTestRunMemorySnapshot(testRun, _startMemorySnapshot, Client.MemorySnapshot.SnapshotProcess());

                var externalLinks = new[]
                {
                    new Client.ExternalLink( "Full Results", $"http://analysis.borsuksoftware.co.uk/links/{random.Next(128*1024) + 1000000}", "Analysis results" )
                };
                await testRun.PublishTestRunExternalLinks(externalLinks);
            }
        }

        private async Task PublishRiskTests(Client.ITestRunSet testRunSet)
        {
            var flattener = FOCSUtils.GetObjectFlattenerForRisk();
            var comparer = FOCSUtils.GetObjectComparerForRisk();

            var risksToCompare = new (string PortfolioName, Models.ResultSet Results, Action<Models.ResultSet> TransformAction)[]
            {
                ("FX\\Vanillas", new Models.ResultSet
                {
                    TradeLevelResults = new []
                    {
                        new Models.TradeOrPositionLevelResults
                        {
                            Book = "Trade #1",
                            Risks = new Models.RiskSet
                            {
                                ValueAtoms = new [] { new Models.ValueAtom { Currency = "GBP", Value = 2341.5} },
                                FXDeltaAtoms = new [] { new Models.FXDeltaAtom { Currency = "GBP", Value = 2341.5} },
                                FXVegaAtoms = new [] { new Models.FXVegaAtom { CurrencyPair = "EURGBP", Value = 233, Expiry = new DateTime( 2023, 06,19), Currency = "GBP"} },
                                FXGammaAtoms = new [] { new Models.FXGammaAtom {  CurrencyPair = "EURGBP", Value = 12, Currency = "GBP"} }
                            },
                            TradeID = "Sample EURGBP vanilla",
                            TradeType = "FXOption"
                        },
                        new Models.TradeOrPositionLevelResults
                        {
                            Book = "Trade #1",
                            Risks = new Models.RiskSet
                            {
                                ValueAtoms = new [] { new Models.ValueAtom { Currency = "USD", Value = 2341.5} },
                                FXDeltaAtoms = new [] { new Models.FXDeltaAtom { Currency = "USD", Value = 2341.5} },
                                FXVegaAtoms = new [] { new Models.FXVegaAtom { CurrencyPair = "GBPUSD", Value = 233, Expiry = new DateTime( 2023, 06,19), Currency = "USD"} },
                                FXGammaAtoms = new [] { new Models.FXGammaAtom {  CurrencyPair = "GBPUSD", Value = 12, Currency = "USD"} }
                            },
                            TradeID = "Sample GBPUSD vanilla",
                            TradeType = "FXOption"
                        }
                    }
                },
                (Models.ResultSet rs ) => { 
                    foreach( var pair in rs.TradeLevelResults.Where( tlr => tlr.Risks.FXVegaAtoms?.Count> 0 ))
                    {
                        foreach( var vegaAtom in pair.Risks.FXVegaAtoms.Where( atom => atom.CurrencyPair == "EURGBP" ))
                            vegaAtom.Value += 4.5;
                    }
                }),
                ("FX\\Exotics\\Europe", new Models.ResultSet
                {
                    TradeLevelResults = new []
                    {
                        new Models.TradeOrPositionLevelResults
                        {
                            Book = "Trade #1",
                            Risks = new Models.RiskSet
                            {
                                ValueAtoms = new [] { new Models.ValueAtom { Currency = "GBP", Value = 2341.5} },
                                FXDeltaAtoms = new [] { new Models.FXDeltaAtom { Currency = "GBP", Value = 2341.5} },
                                FXVegaAtoms = new [] {
                                    new Models.FXVegaAtom { CurrencyPair = "EURGBP", Value = 68, Expiry = new DateTime( 2022,06,19), Currency = "GBP"},
                                    new Models.FXVegaAtom { CurrencyPair = "EURGBP", Value = 168, Expiry = new DateTime( 2022,09,23), Currency = "GBP"},
                                    new Models.FXVegaAtom { CurrencyPair = "EURGBP", Value = 233, Expiry = new DateTime( 2023,06,19), Currency = "GBP"} 
                                },
                                FXGammaAtoms = new [] { new Models.FXGammaAtom {  CurrencyPair = "EURGBP", Value = 12, Currency = "GBP"} }
                            },
                            TradeID = "Sample EURGBP barrier",
                            TradeType = "FXBarrierOption"
                        },
                        new Models.TradeOrPositionLevelResults
                        {
                            Book = "Trade #1",
                            Risks = new Models.RiskSet
                            {
                                ValueAtoms = new [] { new Models.ValueAtom { Currency = "USD", Value = 2341.5} },
                                FXDeltaAtoms = new [] { new Models.FXDeltaAtom { Currency = "USD", Value = 2341.5} },
                                FXVegaAtoms = new [] { new Models.FXVegaAtom { CurrencyPair = "GBPUSD", Value = 233, Expiry = new DateTime( 2023, 06,19), Currency = "USD"} },
                                FXGammaAtoms = new [] { new Models.FXGammaAtom {  CurrencyPair = "GBPUSD", Value = 12, Currency = "USD"} }
                            },
                            TradeID = "Sample GBPUSD vanilla",
                            TradeType = "FXOption"
                        }
                    }
                },
                (Models.ResultSet rs ) => {
                    foreach( var pair in rs.TradeLevelResults.Where( tlr => tlr.Risks.FXVegaAtoms?.Count> 0 ))
                    {
                        foreach( var vegaAtom in pair.Risks.FXVegaAtoms.Where( atom => atom.CurrencyPair == "EURGBP" ))
                            vegaAtom.Value += 4.5;
                    }
                }),
                ("FX\\Exotics\\Asia", new Models.ResultSet
                {
                    TradeLevelResults = new []
                    {
                        new Models.TradeOrPositionLevelResults
                        {
                            Book = "Trade #1",
                            Risks = new Models.RiskSet
                            {
                                ValueAtoms = new [] { new Models.ValueAtom { Currency = "NZD", Value = 12341.5} },
                                FXDeltaAtoms = new [] { new Models.FXDeltaAtom { Currency = "NZD", Value = 12341.5} },
                                FXVegaAtoms = new [] {
                                    new Models.FXVegaAtom { CurrencyPair = "AUDNZD", Value = 681, Expiry = new DateTime( 2022,06,19), Currency = "NZD"},
                                    new Models.FXVegaAtom { CurrencyPair = "AUDNZD", Value = 2318, Expiry = new DateTime( 2022,09,23), Currency = "NZD"},
                                    new Models.FXVegaAtom { CurrencyPair = "AUDNZD", Value = 2336, Expiry = new DateTime( 2023,06,19), Currency = "NZD"}
                                },
                                FXGammaAtoms = new [] { new Models.FXGammaAtom {  CurrencyPair = "AUDNZD", Value = 12, Currency = "NZD"} }
                            },
                            TradeID = "Sample AUDNZD barrier",
                            TradeType = "FXBarrierOption"
                        },
                        new Models.TradeOrPositionLevelResults
                        {
                            Book = "Trade #1",
                            Risks = new Models.RiskSet
                            {
                                ValueAtoms = new [] { new Models.ValueAtom { Currency = "AUDUSD", Value = 23411.5} },
                                FXDeltaAtoms = new [] { new Models.FXDeltaAtom { Currency = "AUDUSD", Value = 23411.5} },
                                FXVegaAtoms = new [] { new Models.FXVegaAtom { CurrencyPair = "AUDUSD", Value = 233, Expiry = new DateTime( 2023, 06,19), Currency = "USD"} },
                                FXGammaAtoms = new [] { new Models.FXGammaAtom {  CurrencyPair = "AUDUSD", Value = 1211, Currency = "USD"} }
                            },
                            TradeID = "Sample AUDUSD vanilla",
                            TradeType = "FXOption"
                        }
                    }
                },
                (Models.ResultSet rs ) => {
                    foreach( var pair in rs.TradeLevelResults.Where( tlr => tlr.Risks.FXVegaAtoms?.Count> 0 ))
                    {
                        foreach( var vegaAtom in pair.Risks.FXVegaAtoms.Where( atom => atom.CurrencyPair == "EURGBP" ))
                            vegaAtom.Value += 4.5;
                    }
                })
            };

            var random = new Random();
            var noDifferences = new string[] { "FX\\Americas\\Flow", "FX\\Americas\\Exotics", "EMG\\Credit\\Asia", "EMG\\Credit\\EMEA", "EMG\\FX\\Africa", "Rates\\Swaps\\Europe", "Rates\\Swaps\\Asia", "Rates\\Exotics\\Europe", "Rates\\Exotics\\Americas" };
            var totalSetToCompare = risksToCompare.Concat(
                noDifferences.Select<string, (string PortfolioName, Models.ResultSet Results, Action<Models.ResultSet> TransformAction)>(name =>
                (PortfolioName: name,
                Results: new Models.ResultSet
                {
                    TradeLevelResults = Enumerable.Range(0, 500 + random.Next(500)).Select(idx => new Models.TradeOrPositionLevelResults { TradeID = $"Trade #{idx}", TradeType = "FXOption", Risks = new Models.RiskSet() }).ToList()
                },
                    TransformAction: (Models.ResultSet rs) => { }
                )));

            foreach( var tuple in totalSetToCompare )
            {
                var expectedResults = tuple.Results.TradeLevelResults.ToDictionary(
                    pair => pair.TradeID,
                    pair => (
                        ValueAtoms: pair.Risks.ValueAtoms == null ? (IReadOnlyCollection<KeyValuePair<string, object>>)Array.Empty<KeyValuePair<string, object>>() : flattener.FlattenObject(null, pair.Risks.ValueAtoms).ToList(),
                        FxDeltaAtoms: pair.Risks.FXDeltaAtoms == null ? (IReadOnlyCollection<KeyValuePair<string, object>>)Array.Empty<KeyValuePair<string, object>>() : flattener.FlattenObject(null, pair.Risks.FXDeltaAtoms).ToList(),
                        FxGammaAtoms: pair.Risks.FXGammaAtoms == null ? (IReadOnlyCollection<KeyValuePair<string, object>>)Array.Empty<KeyValuePair<string, object>>() : flattener.FlattenObject(null, pair.Risks.FXGammaAtoms).ToList(),
                        FxVegaAtoms: pair.Risks.FXVegaAtoms == null ? (IReadOnlyCollection<KeyValuePair<string, object>>)Array.Empty<KeyValuePair<string, object>>() : flattener.FlattenObject(null, pair.Risks.FXVegaAtoms).ToList()
                    ));

                tuple.TransformAction(tuple.Results);

                var actualResults = tuple.Results.TradeLevelResults.ToDictionary(
                    pair => pair.TradeID,
                    pair => (
                        ValueAtoms: pair.Risks.ValueAtoms == null ? (IReadOnlyCollection<KeyValuePair<string, object>>)Array.Empty<KeyValuePair<string, object>>() : flattener.FlattenObject(null, pair.Risks.ValueAtoms).ToList(),
                        FxDeltaAtoms: pair.Risks.FXDeltaAtoms == null ? (IReadOnlyCollection<KeyValuePair<string, object>>)Array.Empty<KeyValuePair<string, object>>() : flattener.FlattenObject(null, pair.Risks.FXDeltaAtoms).ToList(),
                        FxGammaAtoms: pair.Risks.FXGammaAtoms == null ? (IReadOnlyCollection<KeyValuePair<string, object>>)Array.Empty<KeyValuePair<string, object>>() : flattener.FlattenObject(null, pair.Risks.FXGammaAtoms).ToList(),
                        FxVegaAtoms: pair.Risks.FXVegaAtoms == null ? (IReadOnlyCollection<KeyValuePair<string, object>>)Array.Empty<KeyValuePair<string, object>>() : flattener.FlattenObject(null, pair.Risks.FXVegaAtoms).ToList()
                    ));

                var tr = await FOCSUtils.PerformRiskComparisons(
                    testRunSet,
                    $"Risk\\{tuple.PortfolioName}",
                    "Risk based regression test",
                    comparer,
                    expectedResults,
                    actualResults);

                // TODO - Only upload on tr failure
                if (tr.Status == Client.TestRunStatus.Failed)
                {
                    var expectedResultsStr = System.Text.Json.JsonSerializer.Serialize(tuple.Results, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                    await tr.PublishTestRunAdditionalFile("actualResults.json", "The set of results which were generated", new System.IO.MemoryStream(System.Text.UTF8Encoding.UTF8.GetBytes(expectedResultsStr)));
                }

                await FOCSUtils.PublishTestRunAssemblies(tr);
                await FOCSUtils.PublishTestRunMemorySnapshot(tr, _startMemorySnapshot, Client.MemorySnapshot.SnapshotProcess());

            }
        }

        private async Task PublishMarketTests(Client.ITestRunSet testRunSet)
        {
            var marketObjects = new Dictionary<string, object>();
            marketObjects["DiscountCurve-GBP"] = Models.MarketObjects.YieldCurve.FromLZRates(new DateTime(2022, 01, 20),
                new (DateTime date, double rate)[] {
                    (new DateTime(2022, 01, 27), 0.01),
                    (new DateTime(2022, 02, 3), 0.011),
                    (new DateTime(2022, 02, 20), 0.012),
                    (new DateTime(2022, 03, 20), 0.013),
                    (new DateTime(2022, 04, 20), 0.014),
                    (new DateTime(2022, 07, 20), 0.015),
                    (new DateTime(2022, 10, 20), 0.016),
                });

            marketObjects["DiscountCurve-JPY"] = Models.MarketObjects.YieldCurve.FromLZRates(new DateTime(2022, 01, 20),
                new (DateTime date, double rate)[] {
                    (new DateTime(2022, 01, 27), 0.001),
                    (new DateTime(2022, 02, 3), 0.0011),
                    (new DateTime(2022, 02, 20), 0.0012),
                    (new DateTime(2022, 03, 20), 0.0013),
                    (new DateTime(2022, 04, 20), 0.0014),
                    (new DateTime(2022, 07, 20), 0.0015),
                    (new DateTime(2022, 10, 20), 0.0016),
                });

            marketObjects["DiscountCurve-PLN"] = Models.MarketObjects.YieldCurve.FromLZRates(new DateTime(2022, 01, 20),
                new (DateTime date, double rate)[] {
                    (new DateTime(2022, 01, 27), 0.04),
                    (new DateTime(2022, 02, 3), 0.0412),
                    (new DateTime(2022, 02, 20), 0.0414),
                    (new DateTime(2022, 03, 20), 0.0416),
                    (new DateTime(2022, 04, 20), 0.0418),
                    (new DateTime(2022, 07, 20), 0.0427),
                    (new DateTime(2022, 10, 20), 0.0438),
                });

            // Tweak the zloty curve to demonstrate what happens under a difference scenario
            var modifiedMarketObjects = new Dictionary<string, object>(marketObjects);
            modifiedMarketObjects["DiscountCurve-PLN"] = Models.MarketObjects.YieldCurve.FromLZRates(new DateTime(2022, 01, 20),
                new (DateTime date, double rate)[] {
                    (new DateTime(2022, 01, 27), 0.04),
                    (new DateTime(2022, 02, 3), 0.0412),
                    (new DateTime(2022, 02, 20), 0.0414),
                    (new DateTime(2022, 03, 20), 0.0416),
                    (new DateTime(2022, 04, 20), 0.0419),
                    (new DateTime(2022, 07, 20), 0.0427),
                    (new DateTime(2022, 10, 20), 0.0438),
                });


            var allKeys = marketObjects.Keys.Concat(modifiedMarketObjects.Keys).Distinct();
            var allValues = allKeys.Select(key =>
            {
                bool hasExpected = marketObjects.TryGetValue(key, out var expectedMarketObject);
                bool hasActual = modifiedMarketObjects.TryGetValue(key, out var actualMarketObject);

                return new
                {
                    key,
                    hasExpected,
                    hasActual,
                    expectedMarketObject,
                    actualMarketObject
                };
            });

            var missingMarketObjects = new List<string>();
            var additionalMarketObjects = new List<string>();
            var uncomparableObjects = new List<string>();
            var differences = new Dictionary<string, object>();
            var matchingItems = new List<string>();
            foreach (var tuple in allValues)
            {
                if (tuple.hasActual && !tuple.hasExpected)
                {
                    additionalMarketObjects.Add(tuple.key);
                    continue;
                }

                if (tuple.hasExpected && !tuple.hasActual)
                {
                    missingMarketObjects.Add(tuple.key);
                    continue;
                }

                // In a real world example, we'd have something a bit cleverer to select which comparers to use / base this 
                // off of supported interfaces etc. (so assuming a product migration where historically one class was used to
                // build a curve class and now it's being replaced, you'd need to use a common querying interface).
                //
                // Note that the alternative approach is to store the result of the queries rather than the actual objects themselves
                // As this would make it easier to ensure that changes in the class functionality itself were captured.
                if (tuple.actualMarketObject is Models.MarketObjects.YieldCurve actualYieldCurve &&
                    tuple.expectedMarketObject is Models.MarketObjects.YieldCurve expectedYieldCurve)
                {
                    var comparer = new Comparers.YieldCurveComparer();
                    var difference = comparer.CompareYieldCurves(expectedYieldCurve, actualYieldCurve);

                    if (difference.Passed)
                    {
                        matchingItems.Add(tuple.key);
                    }
                    else
                    {
                        differences[tuple.key] = difference;
                    }

                    continue;
                }

                uncomparableObjects.Add(tuple.key);
            }

            // At this point, we can create the suitable payloads....
            // We're using XML here to show the range of payload styles,

            bool passed = additionalMarketObjects.Count == 0 &&
                missingMarketObjects.Count == 0 &&
                uncomparableObjects.Count == 0 &&
                differences.Count == 0;

            var testRun = await testRunSet.CreateTestRun("Market\\Curves", "Sample market test - curves", "Market", passed ? Client.TestRunStatus.Passed : Client.TestRunStatus.Failed);

            string xmlPayload;
            using (var stringWriter = new System.IO.StringWriter())
            {
                using (var xmlWriter = System.Xml.XmlWriter.Create(stringWriter, new System.Xml.XmlWriterSettings { ConformanceLevel = System.Xml.ConformanceLevel.Fragment }))
                {
                    xmlWriter.WriteStartElement("marketObjectTest");
                    xmlWriter.WriteAttributeString("passed", passed.ToString());

                    // Summary
                    xmlWriter.WriteStartElement("summary");
                    xmlWriter.WriteAttributeString("missingKeys", missingMarketObjects.Count.ToString());
                    xmlWriter.WriteAttributeString("additionalKeys", additionalMarketObjects.Count.ToString());
                    xmlWriter.WriteAttributeString("matching", matchingItems.Count.ToString());
                    xmlWriter.WriteAttributeString("differences", differences.Count.ToString());
                    xmlWriter.WriteAttributeString("uncomparableKeys", uncomparableObjects.Count.ToString());
                    xmlWriter.WriteEndElement();

                    // Missing keys
                    if (missingMarketObjects.Count > 0)
                    {
                        xmlWriter.WriteStartElement("missingKeys");
                        foreach (var key in missingMarketObjects)
                        {
                            xmlWriter.WriteStartElement("key");
                            xmlWriter.WriteValue(key);
                            xmlWriter.WriteEndElement();
                        }
                        xmlWriter.WriteEndElement();
                    }

                    // Additional keys
                    if (missingMarketObjects.Count > 0)
                    {
                        xmlWriter.WriteStartElement("additionalKeys");
                        foreach (var key in additionalMarketObjects)
                        {
                            xmlWriter.WriteStartElement("key");
                            xmlWriter.WriteValue(key);
                            xmlWriter.WriteEndElement();
                        }
                        xmlWriter.WriteEndElement();
                    }

                    // Uncomparable keys
                    if (uncomparableObjects.Count > 0)
                    {
                        xmlWriter.WriteStartElement("uncomparableKeys");
                        foreach (var key in uncomparableObjects)
                        {
                            xmlWriter.WriteStartElement("key");
                            xmlWriter.WriteValue(key);
                            xmlWriter.WriteEndElement();
                        }
                        xmlWriter.WriteEndElement();
                    }

                    // Differences
                    if (differences.Count > 0)
                    {
                        var f = new System.Xml.Serialization.XmlSerializerFactory();
                        xmlWriter.WriteStartElement("differences");
                        foreach (var difference in differences)
                        {
                            xmlWriter.WriteStartElement("difference");
                            xmlWriter.WriteStartElement("key");
                            xmlWriter.WriteValue(difference.Key);
                            xmlWriter.WriteEndElement();

                            xmlWriter.WriteStartElement("details");

                            var xs = f.CreateSerializer(difference.Value.GetType());
                            xs.Serialize(xmlWriter, difference.Value);
                            xmlWriter.WriteEndElement();

                            xmlWriter.WriteEndElement();
                        }
                        xmlWriter.WriteEndElement();
                    }

                    xmlWriter.WriteEndElement();
                }

                xmlPayload = stringWriter.ToString();

                await testRun.PublishTestRunResultsXml(xmlPayload);
            }

            // In the case of failure, simulate uploading the updated expected results if it's determined
            // that the new version is correct
            if (!passed)
            {
                string updatedResultsFilePath = System.IO.Path.GetTempFileName();
                using (var fs = new System.IO.FileStream(updatedResultsFilePath, System.IO.FileMode.Create, System.IO.FileAccess.Write))
                {
                    var xsf = new System.Xml.Serialization.XmlSerializerFactory();

                    using (var xmlWriter = System.Xml.XmlWriter.Create(fs, new System.Xml.XmlWriterSettings { Indent = true, IndentChars = "  ", ConformanceLevel = System.Xml.ConformanceLevel.Fragment }))
                    {
                        xmlWriter.WriteStartElement("marketObjects");

                        foreach (var pair in modifiedMarketObjects)
                        {
                            xmlWriter.WriteStartElement("marketObject");
                            xmlWriter.WriteStartElement("key");
                            xmlWriter.WriteValue(pair.Key);
                            xmlWriter.WriteEndElement();

                            xmlWriter.WriteStartElement("value");

                            if (pair.Value is System.Xml.Serialization.IXmlSerializable xmlSerializable)
                            {
                                xmlSerializable.WriteXml(xmlWriter);
                            }
                            else
                            {
                                var xs = xsf.CreateSerializer(pair.Value.GetType());
                                xs.Serialize(xmlWriter, pair.Value);
                            }

                            xmlWriter.WriteEndElement();

                            xmlWriter.WriteEndElement();
                        }

                        xmlWriter.WriteEndElement();
                    }
                }

                await testRun.PublishTestRunAdditionalFile(
                    "updatedExpectations.xml",
                    "The set of updated expectations",
                    new System.IO.FileStream(updatedResultsFilePath, System.IO.FileMode.Open, System.IO.FileAccess.Read));

                await FOCSUtils.PublishTestRunAssemblies(testRun);
                await FOCSUtils.PublishTestRunMemorySnapshot(testRun, _startMemorySnapshot, Client.MemorySnapshot.SnapshotProcess());
            }
        }
    }
}
