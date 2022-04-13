using System;
using System.Collections.Generic;
using System.Text;

namespace BorsukSoftware.Conical.AutomaticUploader.Services.DataPopulation.FOCS.Models
{
    public class TradeOrPositionLevelResults
    {
        public string TradeID { get; set; }
        public string Book { get; set; }
        public string TradeType { get; set; }
        public RiskSet Risks { get; set; }
    }

    public class ResultSet
    {
        public IReadOnlyList<TradeOrPositionLevelResults> TradeLevelResults { get; set; }
    }
}
