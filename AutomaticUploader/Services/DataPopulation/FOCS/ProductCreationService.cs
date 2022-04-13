using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BorsukSoftware.Conical.AutomaticUploader.Services.DataPopulation.FOCS
{
    public class ProductCreationService
    {
        private Microsoft.Extensions.Options.IOptions<FOCSUploadSettings> _uploadSettings;
        private System.Threading.SemaphoreSlim _singleAccessSemaphore = new System.Threading.SemaphoreSlim(1, 1);

        public ProductCreationService(Microsoft.Extensions.Options.IOptions<FOCSUploadSettings> uploadSettings)
        {
            this._uploadSettings = uploadSettings;
        }

        public async Task<Client.IProduct> EnsureProductExists(Client.IAccessLayer accessLayer)
        {
            if (accessLayer == null)
                throw new ArgumentNullException(nameof(accessLayer));

            await _singleAccessSemaphore.WaitAsync();
            try
            {
                var productName = _uploadSettings.Value.ProductName;

                // We work on the assumption that everything that is needed has been created already...
                var allProducts = await accessLayer.GetProducts();
                var existingProduct = allProducts.FirstOrDefault(p => StringComparer.InvariantCultureIgnoreCase.Compare(productName, p.Name) == 0);
                if (existingProduct != null)
                    return existingProduct;

                // At this point, we're going to create the product, 
                var product = await accessLayer.CreateProduct(productName, "FOCS example");

                // And now... create the test run types
                await product.CreateTestRunType("Market", "Market builder tests", new[] { "resultsXml", "memoryUsage", "assembliesdotnet", "additionalfiles" }, null);
                await product.CreateTestRunType("Static", "Static builder tests", new[] { "resultsXml", "memoryUsage", "assembliesdotnet" }, null);
                await product.CreateTestRunType("Risk", "Risk Calculation tests", new[] { "resultsJson", "memoryUsage", "assembliesdotnet", "additionalfiles" }, null);
                await product.CreateTestRunType("Scenarios", "Scenario runner tests", new[] { "resultsXml", "memoryUsage", "assembliesdotnet" }, null);
                await product.CreateTestRunType("Analysis", "General Analysis functionality tests", new[] { "resultstext", "memoryUsage", "assembliesdotnet", "externalLinks" }, null);

                return product;
            }
            finally
            {
                _singleAccessSemaphore.Release();
            }
        }
    }
}
