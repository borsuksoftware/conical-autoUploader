using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BorsukSoftware.Conical.AutomaticUploader.Services.DataPopulation.FOCS
{
    /// <summary>
    /// Helper methods common to integration / regression tests publishers
    /// </summary>
    internal static class FOCSUtils
    {
        public static BorsukSoftware.ObjectFlattener.IObjectFlattener GetObjectFlattenerForRisk()
        {
            var flattener = new BorsukSoftware.ObjectFlattener.ObjectFlattener
            {
                NoAvailablePluginBehaviour = ObjectFlattener.NoAvailablePluginBehaviour.Throw
            };

            // Add custom flatteners so that the output data structure is keyed off the appropriate values
            flattener.Plugins.Add(new BorsukSoftware.ObjectFlattener.Plugins.FunctionBasedPlugin(
                 (name, obj) => obj is IReadOnlyCollection<Models.ValueAtom>,
                 (f, name, obj) =>
                 {
                     var rocAtoms = obj as IReadOnlyCollection<Models.ValueAtom>;
                     var output = rocAtoms.Select(rocAtom => new KeyValuePair<string, object>(rocAtom.Currency, rocAtom.Value));
                     return output;
                 }));
            flattener.Plugins.Add(new BorsukSoftware.ObjectFlattener.Plugins.FunctionBasedPlugin(
                 (name, obj) => obj is IReadOnlyCollection<Models.FXDeltaAtom>,
                 (f, name, obj) =>
                 {
                     var rocAtoms = obj as IReadOnlyCollection<Models.FXDeltaAtom>;
                     var output = rocAtoms.Select(rocAtom => new KeyValuePair<string, object>(rocAtom.Currency, rocAtom.Value));
                     return output;
                 }));
            flattener.Plugins.Add(new BorsukSoftware.ObjectFlattener.Plugins.FunctionBasedPlugin(
                (name, obj) => obj is IReadOnlyCollection<Models.FXVegaAtom>,
                (f, name, obj) =>
                {
                    var rocAtoms = obj as IReadOnlyCollection<Models.FXVegaAtom>;
                    var output = rocAtoms.Select(rocAtom => new KeyValuePair<string, object>($"{rocAtom.CurrencyPair}-{rocAtom.Expiry:ddMMMyyyy}-{rocAtom.Currency}", rocAtom.Value));
                    return output;
                }));

            flattener.Plugins.Add(new BorsukSoftware.ObjectFlattener.Plugins.ListPlugin());
            flattener.Plugins.Add(new BorsukSoftware.ObjectFlattener.Plugins.StandardPlugin());

            return flattener;
        }

        public static BorsukSoftware.Testing.Comparison.IObjectComparer GetObjectComparerForRisk()
        {
            var comparer = new BorsukSoftware.Testing.Comparison.ObjectComparer
            {
                ObjectComparerMismatchedKeysBehaviour = Testing.Comparison.ObjectComparerMismatchedKeysBehaviour.ReportAsDifference,
                ObjectComparerNoAvailablePluginBehaviour = Testing.Comparison.ObjectComparerNoAvailablePluginBehaviour.ReportAsDifference
            };
            comparer.ComparisonPlugins.Add(new BorsukSoftware.Testing.Comparison.Plugins.SimpleStringComparerPlugin());
            comparer.ComparisonPlugins.Add(new BorsukSoftware.Testing.Comparison.Plugins.DoubleComparerPlugin());
            comparer.ComparisonPlugins.Add(new BorsukSoftware.Testing.Comparison.Plugins.DateTimeComparerPlugin());

            return comparer;
        }

        public static async Task PublishTestRunAssemblies(Client.ITestRun testRun)
        {
            await testRun.PublishTestRunAssemblies(
                System.AppDomain.CurrentDomain.GetAssemblies().
                    Where(a => !a.IsDynamic).
                    Select(a =>
                       new Client.AssemblyDetails(
                           a.GetName().Name,
                           a.GetName().ProcessorArchitecture.ToString(),
                           a.GetName().Version.ToString(),
                           a.GetName().CultureName,
                           a.GetName().GetPublicKeyToken(),
                           a.Location,
                           System.IO.File.GetLastWriteTimeUtc(a.Location))).
                    Cast<Client.IAssemblyDetails>().
                    ToList());
        }

        public static async Task PublishTestRunMemorySnapshot(Client.ITestRun testRun, Client.IMemorySnapshot startMemorySnapshot, Client.IMemorySnapshot endMemorySnapshot)
        {
            await testRun.PublishSimpleMemorySnapshot(new Client.SimpleMemorySnapshot(startMemorySnapshot, endMemorySnapshot));
        }

        /// <summary>
        /// Helper method to provide consistency between comparison functions for regression and integration tests
        /// </summary>
        /// <param name="testRunSet"></param>
        /// <param name="testRunName"></param>
        /// <param name="testRunDescription"></param>
        /// <param name="comparer"></param>
        /// <param name="expectedResults"></param>
        /// <param name="actualResults"></param>
        /// <returns></returns>
        public static async Task<Client.ITestRun> PerformRiskComparisons(
            Client.ITestRunSet testRunSet,
            string testRunName,
            string testRunDescription,
            BorsukSoftware.Testing.Comparison.IObjectComparer comparer,
            IDictionary<string, (IReadOnlyCollection<KeyValuePair<string, object>> ValueAtoms, IReadOnlyCollection<KeyValuePair<string, object>> FXDeltaAtoms, IReadOnlyCollection<KeyValuePair<string, object>> FXGammaAtoms, IReadOnlyCollection<KeyValuePair<string, object>> FXVegaAtoms)> expectedResults,
            IDictionary<string, (IReadOnlyCollection<KeyValuePair<string, object>> ValueAtoms, IReadOnlyCollection<KeyValuePair<string, object>> FXDeltaAtoms, IReadOnlyCollection<KeyValuePair<string, object>> FXGammaAtoms, IReadOnlyCollection<KeyValuePair<string, object>> FXVegaAtoms)> actualResults)
        {
            // Perform the actual comparisons
            var allKeys = expectedResults.Keys.Concat(actualResults.Keys).ToHashSet();
            var allResults = allKeys.Select(key =>
            {
                var hasExpected = expectedResults.TryGetValue(key, out var expectedResultsTuple);
                var hasActual = actualResults.TryGetValue(key, out var actualResultsTuple);

                return new
                {
                    key,
                    hasExpected,
                    hasActual,
                    expectedResultsTuple,
                    actualResultsTuple
                };
            }).ToList();

            var missingResults = allResults.Where(t => !t.hasActual && t.hasExpected).ToList();
            var additionalResults = allResults.Where(t => t.hasActual && !t.hasExpected).ToList();

            var differences = allResults.
                Where(pair => pair.hasExpected && pair.hasActual).
                Select(pair => new
                {
                    pair.key,
                    valueAtomDifferences = comparer.CompareValues(pair.expectedResultsTuple.ValueAtoms, pair.actualResultsTuple.ValueAtoms).ToList(),
                    fxDeltaAtomDifferences = comparer.CompareValues(pair.expectedResultsTuple.FXDeltaAtoms, pair.actualResultsTuple.FXDeltaAtoms).ToList(),
                    fxGammaAtomDifferences = comparer.CompareValues(pair.expectedResultsTuple.FXGammaAtoms, pair.actualResultsTuple.FXGammaAtoms).ToList(),
                    fxVegaAtomDifferences = comparer.CompareValues(pair.expectedResultsTuple.FXVegaAtoms, pair.actualResultsTuple.FXVegaAtoms).ToList()
                }).
                Where(pair => pair.fxDeltaAtomDifferences.Count > 0 || pair.fxGammaAtomDifferences.Count > 0 || pair.fxVegaAtomDifferences.Count > 0 || pair.valueAtomDifferences.Count > 0).
                ToList();

            var passed = differences.Count == 0;

            var tr = await testRunSet.CreateTestRun(testRunName, testRunDescription, "Risk", passed ? Client.TestRunStatus.Passed : Client.TestRunStatus.Failed);

            // Always push a payload
            var payload = new
            {
                summary = new
                {
                    passed = allKeys.Count - (missingResults.Count + additionalResults.Count + differences.Count),
                    missingResults = missingResults.Count,
                    additionalResults = additionalResults.Count,
                    differences = differences.Count,
                },
                missingResults = missingResults.Select(t => t.key),
                additionalResults = additionalResults.Select(t => t.key),
                differences = differences.Select(t => new
                {
                    t.key,
                    valueAtomDifferences = t.valueAtomDifferences.Count == 0 ? null : t.valueAtomDifferences,
                    fxDeltaAtomDifferences = t.fxDeltaAtomDifferences.Count == 0 ? null : t.fxDeltaAtomDifferences,
                    fxGammaAtomDifferences = t.fxGammaAtomDifferences.Count == 0 ? null : t.fxGammaAtomDifferences,
                    fxVegaAtomDifferences = t.fxVegaAtomDifferences.Count == 0 ? null : t.fxVegaAtomDifferences
                })
            };
            var payloadStr = System.Text.Json.JsonSerializer.Serialize(payload, new System.Text.Json.JsonSerializerOptions { WriteIndented = true, DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull });
            await tr.PublishTestRunResultsJson(payloadStr);

            if (!passed)
            {

                // Flatten value atom differences for uploading as appropriate
                var valueAtomDifferences = differences.Where(t => t.valueAtomDifferences.Any()).Select(t => new { t.key, t.valueAtomDifferences }).ToList();
                if (valueAtomDifferences.Any())
                {
                    var tempFile = System.IO.Path.GetTempFileName();
                    using (var fileWriter = new System.IO.StreamWriter(tempFile))
                    {
                        fileWriter.WriteLine("trade,riskkey,expected,actual,comparison");

                        foreach (var tradeLevelDifference in valueAtomDifferences)
                        {
                            foreach (var dif in tradeLevelDifference.valueAtomDifferences)
                            {
                                fileWriter.WriteLine($"{tradeLevelDifference.key},\"{dif.Key}\",{dif.Value.ExpectedValue},{dif.Value.ActualValue},{dif.Value.ComparisonPayload}");
                            }
                        }
                    }
                    await tr.PublishTestRunAdditionalFile("valueDifferences.csv", "", new System.IO.FileStream(tempFile, System.IO.FileMode.Open));
                }

                // Flatten delta atom differences for uploading as appropriate
                var deltaAtomDifferences = differences.Where(t => t.valueAtomDifferences.Any()).Select(t => new { t.key, t.fxDeltaAtomDifferences }).ToList();
                if (deltaAtomDifferences.Any())
                {
                    var tempFile = System.IO.Path.GetTempFileName();
                    using (var fileWriter = new System.IO.StreamWriter(tempFile))
                    {
                        fileWriter.WriteLine("trade,riskkey,expected,actual,comparison");

                        foreach (var tradeLevelDifference in deltaAtomDifferences)
                        {
                            foreach (var dif in tradeLevelDifference.fxDeltaAtomDifferences)
                            {
                                fileWriter.WriteLine($"{tradeLevelDifference.key},\"{dif.Key}\",{dif.Value.ExpectedValue},{dif.Value.ActualValue},{dif.Value.ComparisonPayload}");
                            }
                        }
                    }
                    await tr.PublishTestRunAdditionalFile("fxDeltaDifferences.csv", "", new System.IO.FileStream(tempFile, System.IO.FileMode.Open));
                }

                // Flatten vega atom differences for uploading as appropriate
                var fxVegaAtomDifferences = differences.Where(t => t.fxVegaAtomDifferences.Any()).Select(t => new { t.key, t.fxVegaAtomDifferences }).ToList();
                if (fxVegaAtomDifferences.Any())
                {
                    var tempFile = System.IO.Path.GetTempFileName();
                    using (var fileWriter = new System.IO.StreamWriter(tempFile))
                    {
                        fileWriter.WriteLine("trade,ccypair,expiry,ccy,expected,actual,comparison");

                        foreach (var tradeLevelDifference in fxVegaAtomDifferences)
                        {
                            foreach (var dif in tradeLevelDifference.fxVegaAtomDifferences)
                            {
                                var splitRiskKey = dif.Key.Split('-').Select(ss => $"\"{ss}\"");
                                fileWriter.WriteLine($"{tradeLevelDifference.key},{string.Join(',', splitRiskKey)},{dif.Value.ExpectedValue},{dif.Value.ActualValue},{dif.Value.ComparisonPayload}");
                            }
                        }
                    }
                    await tr.PublishTestRunAdditionalFile("fxVegaDifferences.csv", "", new System.IO.FileStream(tempFile, System.IO.FileMode.Open));
                }
            }

            return tr;
        }
    }
}
