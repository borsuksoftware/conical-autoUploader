using System;
using System.Collections.Generic;
using System.Text;

namespace BorsukSoftware.Conical.AutomaticUploader.Services.DataPopulation.FOCS.Models
{
    public class ValueAtom
    {
        public string Currency { get; set; }
        public double Value { get; set; }
    }

    public class FXDeltaAtom
    {
        public string Currency { get; set; }
        public double Value { get; set; }
    }

    public class FXGammaAtom
    {
        public string CurrencyPair { get; set; }
        public string Currency { get; set; }
        public double Value { get; set; }
    }

    public class FXVegaAtom
    {
        public string CurrencyPair { get; set; }
        public string Currency { get; set; }
        public double Value { get; set; }
        public DateTime Expiry { get; set; }
    }

    public class RiskSet
    {
        public IReadOnlyList<ValueAtom> ValueAtoms { get; set; }

        public IReadOnlyList<FXDeltaAtom> FXDeltaAtoms { get; set; }

        public IReadOnlyList<FXGammaAtom> FXGammaAtoms { get; set; }

        public IReadOnlyList<FXVegaAtom> FXVegaAtoms { get; set; }
    }
}
