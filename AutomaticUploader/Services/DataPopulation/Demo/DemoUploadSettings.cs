using System;
using System.Collections.Generic;
using System.Text;

namespace BorsukSoftware.Conical.AutomaticUploader.Services.DataPopulation.Demo
{
    /// <summary>
    /// Settings class to control the demo population
    /// </summary>
    public class DemoUploadSettings
    {
        public string ProductName { get; set; } = "demo";

        public string DemoTestType { get; set; } = "sample";

        public double UploadTimeOfDayHours { get; set; } = 18;

        public int PreviousDaysToUploadCount { get; set; } = -7;
    }
}
