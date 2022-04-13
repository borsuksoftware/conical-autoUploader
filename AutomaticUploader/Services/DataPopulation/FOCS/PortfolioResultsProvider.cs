using System;
using System.Collections.Generic;
using System.Linq;

namespace BorsukSoftware.Conical.AutomaticUploader.Services.DataPopulation.FOCS
{
    public class PortfolioResultsProvider
    {
        public IEnumerable<(string PortfolioWithHierarchy, Models.ResultSet Results)> GetResults()
        {
            var resultsProvider = new ResultsProvider();
            int tradeID = 10000000;
            Func<string> tradeIDFunc = () => (tradeID++).ToString();

            var dictionary = new Dictionary<string, Models.PortfolioDetails>();

            dictionary["FX\\Exotics\\FX01"] = new Models.PortfolioDetails(new[] {
                new Models.TradeTypeOptions
                {
                    Currency = "GBP",
                    Book = "FXO1-GBPUSD",
                    CurrencyPair = "GBPUSD",
                    TradeCount = 40,
                    TradeType = Models.TradeType.FXOption
                },
                new Models.TradeTypeOptions
                {
                    Currency = "GBP",
                    CurrencyPair = "EURGBP",
                    Book = "FX01-EURGBP",
                    TradeCount = 20,
                    TradeType = Models.TradeType.FXOption
                },
                new Models.TradeTypeOptions
                {
                    Currency = "GBP",
                    CurrencyPair = "EURGBP",
                    Book = "FX01-EURGBP",
                    TradeCount = 20,
                    TradeType = Models.TradeType.FXOption
                },
                new Models.TradeTypeOptions
                {
                    Currency = "GBP",
                    CurrencyPair = "EURGBP",
                    Book = "FX01-EURGBP",
                    TradeCount = 20,
                    TradeType = Models.TradeType.FXBarrierOption
                }
            });

            dictionary["FX\\Exotics\\FX02"] = new Models.PortfolioDetails(new[] {
                new Models.TradeTypeOptions
                {
                    Currency = "JPY",
                    Book = "FXO2-USDJPY",
                    CurrencyPair = "USDJPY",
                    TradeCount = 400,
                    TradeType = Models.TradeType.FXOption
                },
                new Models.TradeTypeOptions
                {
                    Currency = "JPY",
                    CurrencyPair = "EURJPY",
                    Book = "FX02-EURJPY",
                    TradeCount = 200,
                    TradeType = Models.TradeType.FXOption
                },
                new Models.TradeTypeOptions
                {
                    Currency = "JPY",
                    CurrencyPair = "GBPJPY",
                    Book = "FX02-GBPJPY",
                    TradeCount = 20,
                    TradeType = Models.TradeType.FXOption
                },
                new Models.TradeTypeOptions
                {
                    Currency = "JPY",
                    Book = "FX02-EURJPY",
                    TradeCount = 200,
                    TradeType = Models.TradeType.Payment
                },
                new Models.TradeTypeOptions
                {
                    Currency = "JPY",
                    Book = "FX02-GBPJPY",
                    TradeCount = 200,
                    TradeType = Models.TradeType.Payment
                }
            });

            foreach (var pair in new (string book, string ccy)[] { ("GBS1", "GBP"), ("EUS1", "EUR"), ("EUS2", "EUR"), ("JPS1", "JPY") })
            {
                dictionary[$"Rates\\Flow\\{pair.book}"] = new Models.PortfolioDetails(new[] {
                    new Models.TradeTypeOptions
                    {
                        Currency = pair.ccy,
                        Book = $"{pair.book}-{pair.ccy}",
                        TradeCount = 2341,
                        TradeType = Models.TradeType.IRSwap
                    },
                    new Models.TradeTypeOptions
                    {
                        Currency = pair.ccy,
                        Book = $"{pair.book}-{pair.ccy}",
                        TradeCount = 41,
                        TradeType = Models.TradeType.Payment
                    }
                });
            }

            foreach (var pair in new (string book, string[] ccypair)[] { ("EURFX", new[] { "EURPLN", "EURCHF", "EURUSD" }), ("GBPFX", new[] { "EURGBP", "GBPUSD", "GBPJPY" }), ("CHFFX", new[] { "CHFJPY", "USDCHF", "EURCHF", "GBPCHF" }) })
            {
                dictionary[$"FX\\Flow\\{pair.book}"] = new Models.PortfolioDetails(pair.ccypair.SelectMany(ccyPair => new[]
                {
                    new Models.TradeTypeOptions
                    {
                        Currency = ccyPair.Substring( 3),
                        Book = pair.book,
                        CurrencyPair = ccyPair,
                        NotionalSize = 3000000,
                        TradeCount = 162,
                        TradeType = Models.TradeType.FXOption
                    },
                    new Models.TradeTypeOptions
                    {
                        Currency = ccyPair.Substring( 3),
                        Book = pair.book,
                        CurrencyPair = ccyPair,
                        NotionalSize = 3000000,
                        TradeCount = 32,
                        TradeType = Models.TradeType.FXBarrierOption
                    }

                }));
            }

            foreach (var pair in dictionary)
            {

                var r = new Random(pair.Key.GetHashCode() ^ (int)DateTime.Today.ToOADate());

                yield return
                    (
                        pair.Key,
                        new Models.ResultSet
                        {
                            TradeLevelResults = resultsProvider.GenerateResults(pair.Value.TradeTypeDetails, r, tradeIDFunc).ToList()
                        }
                    );
            }
        }
    }
}