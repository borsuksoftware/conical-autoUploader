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
    /// This class uses <see cref="FOCSUtils"/> to perform the actual risk comparisons. The assumption that's used is that there is 
    /// a single results object (<see cref="Models.RiskSet"/>) per trade / position and then there are multiple trades / positions 
    /// (see <see cref="Models.ResultSet"/>). This assumption is taken purely for demonstrative reasons and is not required by the tool.
    /// The actual choice of data structures for the source data is up to the system being tested and then the testing tools should
    /// be adapted accordingly.
    /// 
    /// <para>In a more realistic example, the expectation would be that, for a range of different criteria, e.g. portfolios, calls 
    /// would be made to external sources, i.e. the expected and the candidate instances and then those results would be compared. 
    /// This would mean that to the end user, each portfolio would be an independent test run within the test run set so it would
    /// be very easy, in the case of a subset of portfolios failing, where the differences were.</para>
    /// 
    /// <para>Additionally, depending on how investigations were to be performed, users could choose to upload the comparison results 
    /// in different stripes, e.g. instead of keying by portfolio and then showing all of the differences for PV, delta, vega etc. a
    /// user might wish to upload 'all delta differences', 'all vega differences' as an additional file within the test run set, thus
    /// allowing for very easy confirmation of intentional changes which have a large footprint (e.g. the addition or removal of 
    /// an attribute on risk data etc.)
    /// </para>
    /// 
    /// </remarks>
    public class IntegrationTestsPublisher : Microsoft.Extensions.Hosting.BackgroundService
    {
        private readonly IOptions<ServerOptions> _serverOptions;
        private readonly TelegramService _telegramService;
        private readonly IOptions<FOCSUploadSettings> _focsUploadSettings;
        private readonly IOptions<Services.RequestedLifetimeServiceOptions> _requestedLifetimeServiceOptions;
        private readonly Services.RequestedLifetimeService _requestedLifetimeService;
        private readonly ProductCreationService _productCreationService;

        private readonly Client.IMemorySnapshot _startMemorySnapshot;

        public IntegrationTestsPublisher(IOptions<ServerOptions> serverOptions,
            IOptions<FOCSUploadSettings> focsUploadSettings,
            IOptions<Services.RequestedLifetimeServiceOptions> requestedLifetimeServiceOptions,
            Services.RequestedLifetimeService requestedLifetimeService,
            TelegramService telegramService,
            ProductCreationService productCreationService)
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
            var resultsProvider = new ResultsProvider();
            var portfolioResultsProvider = new PortfolioResultsProvider();

            var url = _serverOptions.Value.Url;
            var httpClient = new System.Net.Http.HttpClient();
            httpClient.BaseAddress = new Uri(url);
            if (!string.IsNullOrEmpty(_serverOptions.Value.AccessToken))
                httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_serverOptions.Value.AccessToken}");
            var restApiService = new Client.REST.ApiService(httpClient);
            var server = new Client.REST.AccessLayer(restApiService);

            var flattener = FOCSUtils.GetObjectFlattenerForRisk();
            var comparer = FOCSUtils.GetObjectComparerForRisk();

            while (true)
            {
                try
                {
                    string productName = _focsUploadSettings.Value.ProductName;

                    var focs = await _productCreationService.EnsureProductExists(server);

                    var endDate = DateTime.Today;
                    var startDate = endDate.AddDays(_focsUploadSettings.Value.IntegrationTestsStartDateOffset);

                    if (DateTime.UtcNow.TimeOfDay.TotalHours < _focsUploadSettings.Value.IntegrationTestsLaunchTimeHours)
                        endDate = endDate.AddDays(-1);

                    var searchResults = await server.SearchTestRunSets(new[] { productName }, new[] { Client.TestRunSetStatus.Standard }, null, null, null, startDate, endDate.AddDays(1), null, null, new[] { "integrationtests" });
                    var existingDates = searchResults.Results.Where(trs => trs.Tags.Contains("integrationtests")).Select(trs => trs.RefDate).Distinct().ToHashSet();

                    for (DateTime refDate = startDate; refDate <= endDate; refDate = refDate.AddDays(1))
                    {
                        // Check to see if we already have integration test data
                        if (existingDates.Contains(refDate))
                            continue;

                        // We don't test on the weekend
                        if (refDate.DayOfWeek == DayOfWeek.Saturday || refDate.DayOfWeek == DayOfWeek.Sunday)
                            continue;

                        await _telegramService.SendMessage($"FOCS - Publishing integration results for {refDate:dd-MM-yyyy}");

                        var allPerturbationSettings = new[] { new PerturbationSettings { Name = null } }.Concat(_focsUploadSettings.Value.IntegrationTestsPerturbationSettings);
                        foreach (var perturbationSettings in allPerturbationSettings)
                        {
                            // Upload some data...
                            var trsDescription = "Multi-portfolio integration tests";
                            if (!string.IsNullOrEmpty(perturbationSettings.Name))
                                trsDescription = $"{trsDescription} - {perturbationSettings.Name}";
                            var trs = await focs.CreateTestRunSet("Integration Tests",
                                 trsDescription,
                                refDate,
                                DateTime.UtcNow,
                                new[] { "integrationtests", "prerelease" });

                            foreach (var resultsTuple in portfolioResultsProvider.GetResults())
                            {
                                var expectedResults = resultsTuple.Results.TradeLevelResults.ToDictionary(
                                    val => val.TradeID,
                                    val => 
                                    (
                                        ValueAtoms : val.Risks.ValueAtoms == null ? (IReadOnlyCollection<KeyValuePair<string, object>>)Array.Empty<KeyValuePair<string, object>>() : flattener.FlattenObject(null, val.Risks.ValueAtoms).ToList(),
                                        FxDeltaAtoms : val.Risks.FXDeltaAtoms == null ? (IReadOnlyCollection<KeyValuePair<string, object>>)Array.Empty<KeyValuePair<string, object>>() : flattener.FlattenObject(null, val.Risks.FXDeltaAtoms).ToList(),
                                        FxGammaAtoms : val.Risks.FXGammaAtoms == null ? (IReadOnlyCollection<KeyValuePair<string, object>>)Array.Empty<KeyValuePair<string, object>>() : flattener.FlattenObject(null, val.Risks.FXGammaAtoms).ToList(),
                                        FxVegaAtoms : val.Risks.FXVegaAtoms == null ? (IReadOnlyCollection<KeyValuePair<string, object>>)Array.Empty<KeyValuePair<string, object>>() : flattener.FlattenObject(null, val.Risks.FXVegaAtoms).ToList()
                                    ));

                                // Simulate changes...
                                if (perturbationSettings.CcyPairRegExp != null)
                                {
                                    var regExp = new System.Text.RegularExpressions.Regex(perturbationSettings.CcyPairRegExp, System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                                    foreach (var tlrWithFXVega in resultsTuple.Results.TradeLevelResults.Where(r => r.Risks.FXVegaAtoms?.Count == 1 && r.Risks.FXVegaAtoms.Any(d => regExp.IsMatch(d.CurrencyPair)) == true))
                                    {
                                        foreach (var vegaAtom in tlrWithFXVega.Risks.FXVegaAtoms)
                                        {
                                            vegaAtom.Value *= perturbationSettings.VegaChangeScalar;
                                            vegaAtom.Value += perturbationSettings.VegaChangeOffset;
                                        }
                                    }

                                    foreach (var tlrWithFXVega in resultsTuple.Results.TradeLevelResults.Where(r => r.Risks.FXVegaAtoms?.Count > 1 && r.Risks.FXVegaAtoms.All(d => regExp.IsMatch(d.CurrencyPair))))
                                    {
                                        foreach (var vegaAtom in tlrWithFXVega.Risks.FXVegaAtoms)
                                        {
                                            vegaAtom.Value *= perturbationSettings.VegaChangeScalar;
                                            vegaAtom.Value += perturbationSettings.VegaChangeOffset;
                                        }
                                    }
                                }

                                // Flatten again
                                var actualResults = resultsTuple.Results.TradeLevelResults.ToDictionary(
                                    val => val.TradeID,
                                    val => 
                                    (
                                        ValueAtoms : val.Risks.ValueAtoms == null ? (IReadOnlyCollection<KeyValuePair<string, object>>)Array.Empty<KeyValuePair<string, object>>() : flattener.FlattenObject(null, val.Risks.ValueAtoms).ToList(),
                                        FxDeltaAtoms : val.Risks.FXDeltaAtoms == null ? (IReadOnlyCollection<KeyValuePair<string, object>>)Array.Empty<KeyValuePair<string, object>>() : flattener.FlattenObject(null, val.Risks.FXDeltaAtoms).ToList(),
                                        FxGammaAtoms : val.Risks.FXGammaAtoms == null ? (IReadOnlyCollection<KeyValuePair<string, object>>)Array.Empty<KeyValuePair<string, object>>() : flattener.FlattenObject(null, val.Risks.FXGammaAtoms).ToList(),
                                        FxVegaAtoms : val.Risks.FXVegaAtoms == null ? (IReadOnlyCollection<KeyValuePair<string, object>>)Array.Empty<KeyValuePair<string, object>>() : flattener.FlattenObject(null, val.Risks.FXVegaAtoms).ToList()
                                    ));

                                var tr = await FOCSUtils.PerformRiskComparisons(
                                    trs,
                                    resultsTuple.PortfolioWithHierarchy,
                                    "Integration Test",
                                    comparer,
                                    expectedResults, 
                                    actualResults);

                                // TODO - Only upload on tr failure
                                if (tr.Status == Client.TestRunStatus.Failed)
                                {
                                    var expectedResultsStr = System.Text.Json.JsonSerializer.Serialize(resultsTuple.Results, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                                    await tr.PublishTestRunAdditionalFile("actualResults.json", "The set of results which were generated", new System.IO.MemoryStream(System.Text.UTF8Encoding.UTF8.GetBytes(expectedResultsStr)));
                                }

                                await FOCSUtils.PublishTestRunAssemblies(tr);
                                await FOCSUtils.PublishTestRunMemorySnapshot(tr, _startMemorySnapshot, Client.MemorySnapshot.SnapshotProcess());
                            }

                            await trs.SetStatus(Client.TestRunSetStatus.Standard);
                        }
                    }

                    // Wait for a minute before seeing if there's anything else left to do
                    await Task.Delay(60000);
                }
                catch(Exception ex )
                {
                    await _telegramService.SendMessage($"FOCS Integration Tests Publisher - Exception caught - {ex}");
                }

                if (_requestedLifetimeServiceOptions.Value.Mode == RequestedLifetimeMode.OneOff)
                {
                    Console.WriteLine("FOCS - Integration - Single operation mode - registering completion");
                    _requestedLifetimeService.RegisterPopulationFinish();
                    return;
                }
            }
        }
    }
}
