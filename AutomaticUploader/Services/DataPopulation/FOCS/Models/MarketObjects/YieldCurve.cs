using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using System.Xml.Schema;

namespace BorsukSoftware.Conical.AutomaticUploader.Services.DataPopulation.FOCS.Models.MarketObjects
{
    /// <summary>
    /// Fake yield curve to demonstrate principles of 
    /// </summary>
    /// <remarks>This should not be taken to be a great way to define a yield curve!</remarks>
    public class YieldCurve : System.Xml.Serialization.IXmlSerializable
    {
        #region Data Model

        public DateTime StartDate { get; }
        public IReadOnlyList<(DateTime PointDate, double DiscountFactor)> DiscountFactors { get; }
        public DateTime EndDate => this.DiscountFactors[this.DiscountFactors.Count - 1].PointDate;

        #endregion

        public YieldCurve(DateTime startDate, IEnumerable<(DateTime PointDate, double DiscountFactor)> discountFactors)
        {
            this.StartDate = startDate;
            this.DiscountFactors = discountFactors.ToList();

            // Do some sanity checks so that the algorithm isn't too bad
            for (int i = 1; i < this.DiscountFactors.Count; ++i)
            {
                if (this.DiscountFactors[i].PointDate <= this.DiscountFactors[i - 1].PointDate)
                    throw new InvalidOperationException($"Point #{i} (date = {this.DiscountFactors[i].PointDate}) cannot be on or before the previous point (date = {this.DiscountFactors[i - 1].PointDate})");
            }
        }

        /// <summary>
        /// Get the discount factor for the given date
        /// </summary>
        /// <param name="date"></param>
        /// <returns></returns>
        public double GetDiscountFactor(DateTime fromDate, DateTime toDate)
        {
            var fromDateDF = GetDiscountFactorFromStartDate(fromDate);
            var toDateDF = GetDiscountFactorFromStartDate(toDate);

            var df = toDateDF / fromDateDF;
            return df;
        }

        #region Implementation details

        private double GetDiscountFactorFromStartDate(DateTime date)
        {
            // DF = e(-rT)
            // => r = ln(DF) / -T
            if (date <= this.StartDate)
                return 1.0;

            if (date > this.DiscountFactors[this.DiscountFactors.Count - 1].PointDate)
            {
                var lastPoint = this.DiscountFactors[this.DiscountFactors.Count - 1];
                var tLastEndDate = (lastPoint.PointDate.ToOADate() - this.StartDate.ToOADate()) / 365.25;
                var rLastEndDate = Math.Log(lastPoint.DiscountFactor) / -tLastEndDate;

                // We need to extrapolate, but can assume that the rate is then constant...
                var additionalT = (date.ToOADate() - lastPoint.PointDate.ToOADate()) / 365.25;
                var additionalDFFromLastPointToTargetDate = Math.Exp(-rLastEndDate * additionalT);

                var totalDF = lastPoint.DiscountFactor * additionalDFFromLastPointToTargetDate;
                return totalDF;
            }

            if (date < this.DiscountFactors[0].PointDate)
            {
                var firstPoint = this.DiscountFactors[0];
                var tFirstDate = (firstPoint.PointDate.ToOADate() - this.StartDate.ToOADate()) / 365.25;
                var rFirstEndDate = Math.Log(firstPoint.DiscountFactor) / -tFirstDate;

                var actualT = (date.ToOADate() - date.ToOADate()) / 365.25;
                var df = Math.Exp(-rFirstEndDate * actualT);
                return df;
            }

            for (int i = 1; i < this.DiscountFactors.Count; i++)
            {
                if (date == this.DiscountFactors[i].PointDate)
                    return this.DiscountFactors[i].DiscountFactor;

                if (date < this.DiscountFactors[i].PointDate)
                {
                    int firstPointIdx = i - 1;
                    int secondPointIdx = i;

                    var tFirstPoint = (this.DiscountFactors[firstPointIdx].PointDate.ToOADate() - this.StartDate.ToOADate()) / 365.25;
                    var rFirstPoint = Math.Log(this.DiscountFactors[firstPointIdx].DiscountFactor) / -tFirstPoint;

                    var tSecondPoint = (this.DiscountFactors[secondPointIdx].PointDate.ToOADate() - this.StartDate.ToOADate()) / 365.25;
                    var rSecondPoint = Math.Log(this.DiscountFactors[secondPointIdx].DiscountFactor) / -tSecondPoint;

                    var tPortion = (date.ToOADate() - this.DiscountFactors[i].PointDate.ToOADate()) / 365.25;
                    var interpolatedR = rFirstPoint + ((rSecondPoint - rFirstPoint) * tPortion / (tSecondPoint - tFirstPoint));
                    var additionalDF = Math.Exp(-interpolatedR * tPortion);

                    var totalDF = this.DiscountFactors[i].DiscountFactor * additionalDF;
                    return totalDF;
                }
            }

            throw new InvalidOperationException("Shouldn't happen");
        }

        #endregion

        public static YieldCurve FromLZRates(DateTime startTime, IEnumerable<(DateTime date, double rate)> rates)
        {
            return new YieldCurve(
                startTime,
                rates.Select(tuple => (PointDate: tuple.date, DiscountFactor: Math.Exp(-tuple.rate * ((tuple.date - startTime).TotalDays / 365.25)))));
        }

        public XmlSchema GetSchema()
        {
            throw new NotImplementedException();
        }

        public void ReadXml(XmlReader reader)
        {
            throw new NotImplementedException();
        }

        public void WriteXml(XmlWriter writer)
        {
            writer.WriteStartElement("yieldCurve");
            writer.WriteAttributeString("version", "1");
            writer.WriteAttributeString("startDate", this.StartDate.ToOADate().ToString());
            writer.WriteStartElement("points");
            foreach (var point in this.DiscountFactors)
            {
                writer.WriteStartElement("point");
                writer.WriteAttributeString("date", point.PointDate.ToOADate().ToString());
                writer.WriteAttributeString("discountFactor", point.DiscountFactor.ToString("r"));
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
            writer.WriteEndElement();
        }
    }
}
