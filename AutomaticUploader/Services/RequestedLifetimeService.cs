using System;
using System.Collections.Generic;
using System.Text;

namespace BorsukSoftware.Conical.AutomaticUploader.Services
{
    public class RequestedLifetimeServiceOptions
    {
        public RequestedLifetimeMode Mode { get; set; } = RequestedLifetimeMode.Continuous;
    }
    public enum RequestedLifetimeMode
    {
        Continuous,
        OneOff,
    }
    public class RequestedLifetimeService
    {
        private readonly Microsoft.Extensions.Hosting.IApplicationLifetime _applicationLifetime;
        private int _outstandingCount = 3;

        public RequestedLifetimeService(Microsoft.Extensions.Hosting.IApplicationLifetime applicationLifetime)
        {
            _applicationLifetime = applicationLifetime;
        }

        public void RegisterPopulationFinish()
        {
            var decrementedResult = System.Threading.Interlocked.Decrement(ref _outstandingCount);
            if (decrementedResult <= 0)
            {
                Console.WriteLine("Requesting application to end");
                _applicationLifetime.StopApplication();
            }
        }
    }
}
