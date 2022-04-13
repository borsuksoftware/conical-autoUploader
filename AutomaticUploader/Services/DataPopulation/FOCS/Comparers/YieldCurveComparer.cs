using System;
using System.Collections.Generic;
using System.Text;

namespace BorsukSoftware.Conical.AutomaticUploader.Services.DataPopulation.FOCS.Comparers
{
    public class YieldCurveComparer
    {
        public Models.MarketObjects.YieldCurveDifferences CompareYieldCurves( Models.MarketObjects.YieldCurve expected, Models.MarketObjects.YieldCurve actual )
        {
            if (expected == null)
                throw new ArgumentNullException(nameof(expected));

            if (actual == null)
                throw new ArgumentNullException(nameof(actual));

            // Here, making a choice that we're only really interested in the range that the expected curve supports,
            // if the new curve adds more support, then treat that as a pass. This is an implementation choice. Real
            // use-cases may vary
            var startDate = expected.StartDate;
            var endDate = expected.EndDate;

            var differences = new List<Models.MarketObjects.YieldDiscountFactorDifference>();
            for(var date = startDate ; date<= endDate; date = date.AddDays(1) )
            {
                // Only check week days - clearly, for middle eastern currencies, this would need to be a function of
                // the currency's conventions etc.
                if (date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday)
                    continue;

                var expectedDF = expected.GetDiscountFactor(startDate, date);
                var actualDF = actual.GetDiscountFactor(startDate, date);

                if (expectedDF != actualDF)
                    differences.Add(new Models.MarketObjects.YieldDiscountFactorDifference { Date = date, ExpectedDiscountFactor = expectedDF, ActualDiscountFactor = actualDF });
            }

            var output = new Models.MarketObjects.YieldCurveDifferences
            {
                StartDate = startDate,
                EndDate = endDate,
                DiscountFactorDifferences = differences
            };

            return output;
        }
    }
}
