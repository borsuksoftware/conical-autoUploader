using System;
using System.Collections.Generic;
using System.Text;

namespace BorsukSoftware.Conical.AutomaticUploader.Services.DataPopulation.FOCS.Models
{
    public enum TradeType
    {
        IRSwap,
        FXOption,
        FXBarrierOption,
        Payment,

    }

    public class TradeTypeOptions
    {
        public TradeType TradeType { get; set; }
        public string Book { get; set; }
        public double NotionalSize { get; set; } = 1_000_000;
        public int TradeCount { get; set; } = 1;
        public string Currency { get; set; }
        public string CurrencyPair { get; set; }
    }

    public class PortfolioDetails
    {
        public List<TradeTypeOptions> TradeTypeDetails { get; } = new List<TradeTypeOptions>();

        public PortfolioDetails( IEnumerable<TradeTypeOptions> tradeTypeOptions)
        {
            foreach (var val in tradeTypeOptions)
                this.TradeTypeDetails.Add(val);
        }
    }
}
