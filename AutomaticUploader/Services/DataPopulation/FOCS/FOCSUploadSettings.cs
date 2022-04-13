using System;
using System.Collections.Generic;
using System.Text;

namespace BorsukSoftware.Conical.AutomaticUploader.Services.DataPopulation.FOCS
{
    /// <summary>
    /// Class used by the upload tool 
    /// </summary>
    public class FOCSUploadSettings
    {

        public string ProductName { get; set; } = "FOCS";

        /// <summary>
        /// Get / set the number of days for which regression test data should be uploaded for
        /// </summary>
        public int RegressionTestsStartDateOffset { get; set; } = -14;

        /// <summary>
        /// Get / set the number of days for which integration test data should be uploaded for
        /// </summary>
        public int IntegrationTestsStartDateOffset { get; set; } = -14;

        public PerturbationSettings[] IntegrationTestsPerturbationSettings { get; set; } = Array.Empty<PerturbationSettings>();

        public double IntegrationTestsLaunchTimeHours { get; set; } = 18;

        public double RegressionTestsLaunchTimeHours { get; set; } = 18;
    }

    public class PerturbationSettings
    {
        public string Name { get; set; }
        public string CcyPairRegExp { get; set; }
        public double VegaChangeScalar { get; set; } = 1.0;
        public double VegaChangeOffset { get; set; } = 0.0;
    }
}
