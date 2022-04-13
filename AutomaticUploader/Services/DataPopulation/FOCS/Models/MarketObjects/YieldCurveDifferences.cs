using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;
using System.Xml.Schema;

namespace BorsukSoftware.Conical.AutomaticUploader.Services.DataPopulation.FOCS.Models.MarketObjects
{
    public class YieldDiscountFactorDifference : System.Xml.Serialization.IXmlSerializable
    {
        public DateTime? Date { get; set; }
        public double ExpectedDiscountFactor { get; set; }
        public double ActualDiscountFactor { get; set; }

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
            writer.WriteStartElement("diff");
            if (this.Date.HasValue)
                writer.WriteAttributeString("date", this.Date.Value.ToString("dd-MMM-yyyy"));

            if (this.ExpectedDiscountFactor != this.ActualDiscountFactor)
            {
                writer.WriteAttributeString("expected", this.ExpectedDiscountFactor.ToString("r"));
                writer.WriteAttributeString("actual", this.ExpectedDiscountFactor.ToString("r"));
                writer.WriteAttributeString("diff", (this.ActualDiscountFactor - this.ExpectedDiscountFactor).ToString("r"));
            }
            writer.WriteEndElement();
        }
    }

    public class YieldCurveDifferences : System.Xml.Serialization.IXmlSerializable
    {
        [System.Xml.Serialization.XmlAttribute("startDate")]
        public DateTime? StartDate { get; set; }

        [System.Xml.Serialization.XmlAttribute("endDate")]
        public DateTime? EndDate { get; set; }

        [System.Xml.Serialization.XmlElement("differences")]
        public IReadOnlyList<YieldDiscountFactorDifference> DiscountFactorDifferences { get; set; }

        [System.Xml.Serialization.XmlAttribute("passed")]
        public bool Passed => this.DiscountFactorDifferences.Count == 0;

        public XmlSchema GetSchema() { throw new NotImplementedException(); }
        public void ReadXml(XmlReader reader) { throw new NotImplementedException(); }

        public void WriteXml(XmlWriter writer)
        {
            writer.WriteStartElement("yieldCurveDifferences");
            if (this.StartDate.HasValue)
                writer.WriteAttributeString("startDate", this.StartDate.Value.ToString("dd-MMM-yyyy"));
            if (this.EndDate.HasValue)
                writer.WriteAttributeString("endDate", this.EndDate.Value.ToString("dd-MMM-yyyy"));

            writer.WriteStartElement("differences");
            foreach (var diff in this.DiscountFactorDifferences)
                diff.WriteXml(writer);
            writer.WriteEndElement();
            writer.WriteEndElement();
        }
    }
}
