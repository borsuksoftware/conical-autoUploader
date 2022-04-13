using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Options;

namespace BorsukSoftware.Conical.AutomaticUploader.Services.DataPopulation.Demo
{
    /// <summary>
    /// Background service to publish, daily, a sample covering all of the available features in the tool
    /// </summary>
    class DataPopulationService : Microsoft.Extensions.Hosting.BackgroundService
    {
        private readonly IOptions<ServerOptions> _serverOptions;
        private readonly IOptions<DemoUploadSettings> _uploadSettings;
        private readonly IOptions<Services.RequestedLifetimeServiceOptions> _requestedLifetimeServiceOptions;
        private readonly Services.RequestedLifetimeService _requestedLifetimeService;
        private readonly TelegramService _telegramService;

        private readonly Client.IMemorySnapshot _startMemorySnapshot;

        public DataPopulationService(
            IOptions<ServerOptions> serverOptions,
            Services.RequestedLifetimeService requestedLifetimeService,
            IOptions<Services.RequestedLifetimeServiceOptions> requestedLifetimeServiceOptions,
            IOptions<DemoUploadSettings> uploadSettings,
            TelegramService telegramService)
        {
            _serverOptions = serverOptions;
            _requestedLifetimeService = requestedLifetimeService;
            _requestedLifetimeServiceOptions = requestedLifetimeServiceOptions;
            _telegramService = telegramService;
            _uploadSettings = uploadSettings;

            _startMemorySnapshot = Client.MemorySnapshot.SnapshotProcess();
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await Task.Delay(0);

            var url = _serverOptions.Value.Url;
            var server = new Client.REST.AccessLayer(url, _serverOptions.Value.AccessToken);

            IReadOnlyCollection<(string RoleName, Client.ProductPrivilege AdditionalPrivilege)> additionalRolePrivileges = null;
            if (!string.IsNullOrEmpty(_serverOptions.Value.UploadRoleName))
                additionalRolePrivileges = new[] { (RoleName: _serverOptions.Value.UploadRoleName, AdditionalPrivilege: Client.ProductPrivilege.Admin) };

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var allProducts = await server.GetProducts();
                    var product = allProducts.FirstOrDefault(p => StringComparer.InvariantCultureIgnoreCase.Compare(p.Name, _uploadSettings.Value.ProductName) == 0);
                    if (product == null)
                    {
                        Console.WriteLine($"Unable to find product '{_uploadSettings.Value.ProductName}', creating");

                        try
                        {
                            product = await server.CreateProduct(_uploadSettings.Value.ProductName, "Auto-generated");

                            await product.CreateTestRunType(
                                _uploadSettings.Value.DemoTestType,
                                "Sample test type covering most result types",
                                new[] {
                                    "ResultsText",
                                    "ResultsXml",
                                    "ResultsJson",
                                    "ResultsCsv",
                                    "ResultsTsv",
                                    "MemoryUsage",
                                    "AssembliesDotNet",
                                    "Logs",
                                    "AdditionalFiles",
                                    "ExternalLinks"
                                },
                                null);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Exception caught creating new demo product - {ex}");

                            if (_requestedLifetimeServiceOptions.Value.Mode == RequestedLifetimeMode.OneOff)
                            {
                                Console.WriteLine("Single operation mode - registering completion");
                                _requestedLifetimeService.RegisterPopulationFinish();
                            }

                            return;
                        }
                    }

                    var refDate = DateTime.Today;
                    var startDate = refDate.AddDays(_uploadSettings.Value.PreviousDaysToUploadCount);
                    var endDate = DateTime.Today;
                    if (DateTime.UtcNow.TimeOfDay.TotalHours < _uploadSettings.Value.UploadTimeOfDayHours)
                        endDate = endDate.AddDays(-1);

                    var searchResults = await server.SearchTestRunSets(new[] { product.Name },
                        new[] { Client.TestRunSetStatus.Standard },
                        null,
                        null,
                        null,
                        startDate,
                        null,
                        null,
                        null,
                        null);

                    var existingDates = searchResults.Results.Select(trs => trs.RefDate).ToHashSet();

                    for (var date = startDate; date <= endDate; date = date.AddDays(1))
                    {
                        if (existingDates.Contains(date))
                            continue;

                        await _telegramService.SendMessage($"Uploading demo data for {date:dd-MMM-yyyy}");


                        var testRunSet = await product.CreateTestRunSet("Daily Demo", "Demo TRS to show all features", date, DateTime.UtcNow, null);

                        // Test Run Set level files
                        {
                            using (var stream = new System.IO.MemoryStream(Resources.Resources.Graph))
                                await testRunSet.PublishAdditionalFile("Graph.png", "Swagger file", stream);

                            using (var stream = new System.IO.MemoryStream(Resources.Resources.Analysis))
                                await testRunSet.PublishAdditionalFile("Analysis.png", "Swagger file", stream);

                            using (var stream = new System.IO.MemoryStream(Resources.Resources.Calculation))
                                await testRunSet.PublishAdditionalFile("Calculation.png", "Swagger file", stream);
                        }

                        // TODO - Add comments at a TSR level
                        // TODO - Add external links at a TSR level

                        var testRun = await testRunSet.CreateTestRun("Sample", "Sample", _uploadSettings.Value.DemoTestType, Client.TestRunStatus.Passed);

                        using (var additionalFileStream = new System.IO.MemoryStream(Resources.Resources.Graph))
                            await testRun.PublishTestRunAdditionalFile("default.png", "Optional", additionalFileStream);

                        // Handle logs
                        {
                            var logMesssages = Enumerable.Range(0, 10000).
                                Select(idx => $"This is log message #{idx}");

                            await testRun.PublishTestRunLogMessages(logMesssages);
                        }

                        // Handle CSV
                        {
                            var csvDataResourceName = this.GetType().Assembly.GetManifestResourceNames().FirstOrDefault(n => n.EndsWith("Resources.SampleData.csv", StringComparison.OrdinalIgnoreCase));
                            if (!string.IsNullOrEmpty(csvDataResourceName))
                            {
                                using (var stream = this.GetType().Assembly.GetManifestResourceStream(csvDataResourceName))
                                {
                                    using (var streamReader = new System.IO.StreamReader(stream))
                                    {
                                        var csvData = streamReader.ReadToEnd();
                                        await testRun.PublishTestRunResultsCsv(csvData);
                                    }
                                }
                            }
                        }

                        // Handle TSV
                        {
                            var randomTsv = new System.Random(300);
                            int tradeID = 500000;
                            var rows = Enumerable.Range(0, 600).
                                Select(i =>
                                {
                                    tradeID += randomTsv.Next(4) + 1;

                                    double expected, actual, dif;
                                    if (randomTsv.NextDouble() < 0.02)
                                    {
                                        expected = (randomTsv.NextDouble() - 0.5) * 750000;
                                        actual = expected + ((randomTsv.NextDouble() - 0.5) * 50000);
                                        dif = actual - expected;
                                    }

                                    else
                                    {
                                        expected = actual = (randomTsv.NextDouble() - 0.5) * 750000;
                                        dif = 0;

                                    }

                                    return $"{i}\t{tradeID}\t{expected}\t{actual}\t{dif}";
                                });

                            var headerRow = "IDX\tTrade ID\tExpected\tActual\tDif";

                            var joined = new[] { headerRow }.Concat(rows);
                            var allText = string.Join('\n', joined);

                            await testRun.PublishTestRunResultsTsv(allText);
                        }

                        // Results Text
                        {
                            var sb = new StringBuilder();
                            for (int i = 0; i < 200; ++i)
                            {
                                sb.Append($"This is line #{i}, there could be some really interesting stuff here");
                                sb.AppendLine();
                            }

                            var text = sb.ToString();

                            await testRun.PublishTestRunResultsText(text);
                        }

                        // Results JSon
                        {
                            var megaResultObject = new
                            {
                                failures = Enumerable.Range(0, 8).Select(idx => new { key = idx, differences = Enumerable.Range(0, 100).Select(i => new { day = i, value1 = (double)i * 0.8, value2 = (double)i * 0.79 }) })
                            };

                            using (var memStream = new System.IO.MemoryStream())
                            {
                                using (var w = new System.Text.Json.Utf8JsonWriter(memStream))
                                {
                                    System.Text.Json.JsonSerializer.Serialize(w, megaResultObject);
                                }

                                memStream.Position = 0;
                                var streamReader = new System.IO.StreamReader(memStream);
                                var text = streamReader.ReadToEnd();

                                await testRun.PublishTestRunResultsJson(text);
                            }
                        }

                        // Handle XML
                        {
                            var xmlDataResourceName = this.GetType().Assembly.GetManifestResourceNames().FirstOrDefault(n => n.EndsWith("Resources.SampleData.xml", StringComparison.OrdinalIgnoreCase));
                            if (!string.IsNullOrEmpty(xmlDataResourceName))
                            {
                                using (var stream = this.GetType().Assembly.GetManifestResourceStream(xmlDataResourceName))
                                {
                                    using (var streamReader = new System.IO.StreamReader(stream))
                                    {
                                        var xmlData = streamReader.ReadToEnd();
                                        await testRun.PublishTestRunResultsXml(xmlData);
                                    }
                                }
                            }
                        }

                        // Handle external links
                        var links = new Client.ExternalLink[]
                        {
                            new Client.ExternalLink( "Borsuk Software", "http://borsuksoftware.co.uk", "Our corporate site"),
                            new Client.ExternalLink( "Testing Suite", "http://testingsuite.cloud", "Information about the testing suite"),
                        };
                        await testRun.PublishTestRunExternalLinks(links);
                        await testRunSet.PublishExternalLinks(links);

                        await FOCS.FOCSUtils.PublishTestRunAssemblies(testRun);
                        await FOCS.FOCSUtils.PublishTestRunMemorySnapshot(testRun, _startMemorySnapshot, Client.MemorySnapshot.SnapshotProcess());

                        await testRunSet.SetStatus(Client.TestRunSetStatus.Standard);
                    }

                    await Task.Delay(60000, stoppingToken);
                }
                catch (Exception ex)
                {
                    await _telegramService.SendMessage($"Demo uploader - exception caught - {ex}");
                }

                if( _requestedLifetimeServiceOptions.Value.Mode == RequestedLifetimeMode.OneOff )
                {
                    Console.WriteLine("Single operation mode - registering completion");
                    _requestedLifetimeService.RegisterPopulationFinish();
                    return;
                }
            }
        }
    }
}
