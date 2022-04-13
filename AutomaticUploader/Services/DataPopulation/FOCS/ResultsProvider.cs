using System;
using System.Collections.Generic;
using System.Linq;

namespace BorsukSoftware.Conical.AutomaticUploader.Services.DataPopulation.FOCS
{
    public class ResultsProvider
    {
        public IEnumerable<Models.TradeOrPositionLevelResults> GenerateResults(IEnumerable<Models.TradeTypeOptions> tradeTypeOptions, Random r, Func<string> tradeIDValueFunc)
        {
            foreach (var options in tradeTypeOptions)
            {
                switch (options.TradeType)
                {
                    case Models.TradeType.Payment:
                        for (int i = 0; i < options.TradeCount; ++i)
                        {
                            var valueAtoms = new[] { new Models.ValueAtom { Currency = options.Currency, Value = (r.NextDouble() - 0.5) * 2 * options.NotionalSize } };

                            yield return new Models.TradeOrPositionLevelResults
                            {
                                TradeID = tradeIDValueFunc().ToString(),
                                Book = options.Book,
                                TradeType = "Payment",
                                Risks = new Models.RiskSet
                                {
                                    ValueAtoms = valueAtoms,
                                    FXDeltaAtoms = valueAtoms.Select(va => new Models.FXDeltaAtom { Currency = va.Currency, Value = va.Value }).ToList()
                                }
                            };
                        }
                        break;

                    case Models.TradeType.FXOption:
                        for (int i = 0; i < options.TradeCount; ++i)
                        {
                            var valueAtoms = new[] { new Models.ValueAtom { Currency = options.Currency, Value = (r.NextDouble() - 0.5) * 2 * options.NotionalSize } };
                            yield return new Models.TradeOrPositionLevelResults
                            {
                                TradeID = tradeIDValueFunc().ToString(),
                                Book = options.Book,
                                TradeType = "FXOption",
                                Risks = new Models.RiskSet
                                {
                                    ValueAtoms = valueAtoms,
                                    FXDeltaAtoms = valueAtoms.Select(va => new Models.FXDeltaAtom { Currency = va.Currency, Value = va.Value }).ToList(),
                                    FXVegaAtoms = new[] { new Models.FXVegaAtom { Currency = options.Currency, CurrencyPair = options.CurrencyPair, Expiry = DateTime.Today.AddDays(r.Next(400) + 10), Value = (r.NextDouble() - 0.5) * 2 * options.NotionalSize * 0.1 } }.ToList()
                                }
                            };
                        }
                        break;

                    case Models.TradeType.FXBarrierOption:
                        for (int i = 0; i < options.TradeCount; ++i)
                        {
                            var valueAtoms = new[] { new Models.ValueAtom { Currency = options.Currency, Value = (r.NextDouble() - 0.5) * 2 * options.NotionalSize } };

                            int expiryDayCount = r.Next(400) + 10;
                            int[] expiryDayGrid = new int[] { 7, 14, 30, 60, 90, 180, 360, 540, 720 };
                            var vegaAtoms = new List<Models.FXVegaAtom>();
                            foreach (var gridPoint in expiryDayGrid)
                            {
                                vegaAtoms.Add(new Models.FXVegaAtom { Currency = options.Currency, CurrencyPair = options.CurrencyPair, Expiry = DateTime.Today.AddDays(gridPoint), Value = (r.NextDouble() - 0.5) * 2 * options.NotionalSize * 0.1 });
                                if (expiryDayCount < gridPoint)
                                    break;
                            }

                            yield return new Models.TradeOrPositionLevelResults
                            {
                                TradeID = tradeIDValueFunc().ToString(),
                                Book = options.Book,
                                TradeType = "FXBarrierOption",
                                Risks = new Models.RiskSet
                                {
                                    ValueAtoms = valueAtoms,
                                    FXDeltaAtoms = valueAtoms.Select(va => new Models.FXDeltaAtom { Currency = va.Currency, Value = va.Value }).ToList(),
                                    FXVegaAtoms = vegaAtoms
                                }
                            };
                        }
                        break;

                    case Models.TradeType.IRSwap:
                        for( int i = 0; i < options.TradeCount;++i)
                        {
                            yield return new Models.TradeOrPositionLevelResults
                            {
                                TradeID = tradeIDValueFunc().ToString(),
                                Book = options.Book,
                                TradeType = "IRSwaps",
                                Risks = new Models.RiskSet
                                {
                                    ValueAtoms = new[] { new Models.ValueAtom { Currency = options.Currency, Value = (r.NextDouble() - 0.5) * 2 * 0.3 * options.NotionalSize } }
                                }
                            };
                        }
                        break;
                }
            }
        }
    }
}
