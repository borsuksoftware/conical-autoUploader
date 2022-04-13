using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;


namespace BorsukSoftware.Conical.AutomaticUploader
{
    class Program
    {
        private const string CONST_CONFIGBLOCKS_SERVER = "server";
        private const string CONST_CONFIGBLOCKS_TELEGRAM = "telegram";
        private const string CONST_CONFIGBLOCKS_LIFETIME = "lifetime";

        private static void AddConfigBlocks( IConfigurationBuilder configurationBuilder, string [] args)
        {
            configurationBuilder.AddJsonFile("appsettings.json", optional: true);
            configurationBuilder.AddCommandLine(args);

        }

        static int Main(string[] args)
        {
            var configBuilder = new ConfigurationBuilder();
            AddConfigBlocks(configBuilder, args);
            var config = configBuilder.Build();
            var serverOptions = new Services.ServerOptions();
            config.Bind(CONST_CONFIGBLOCKS_SERVER, serverOptions);

            var lifetimeOptions = new Services.RequestedLifetimeServiceOptions();
            config.Bind(CONST_CONFIGBLOCKS_LIFETIME, lifetimeOptions);

            Console.WriteLine("Server options:");
            Console.WriteLine($" url - {serverOptions.Url}");
            Console.WriteLine($" token - {(string.IsNullOrWhiteSpace(serverOptions.AccessToken) ? "No token specified" : serverOptions.AccessToken)})");
            Console.WriteLine($" upload role name - {serverOptions.UploadRoleName}");

            Console.WriteLine();
            Console.WriteLine($"Mode - {lifetimeOptions.Mode}");

            if (string.IsNullOrEmpty(serverOptions.Url))
            {
                Console.WriteLine("No server specified - terminating");
                return -1;
            }

            if( !Uri.TryCreate(serverOptions.Url,  UriKind.Absolute, out _))
            {
                Console.WriteLine($"Invalid server specified, unable to create Uri - {serverOptions.Url}");
                return -1;
            }

            var hostBuilder = new HostBuilder().ConfigureAppConfiguration((hostContext, config) =>
            {
                config.AddJsonFile("appsettings.json", optional: true);
                config.AddCommandLine( args );
            }).ConfigureServices((hostContext, services) =>
                {
                    services.Configure<Services.ServerOptions>(hostContext.Configuration.GetSection(CONST_CONFIGBLOCKS_SERVER));
                    services.Configure<Services.TelegramOptions>(hostContext.Configuration.GetSection(CONST_CONFIGBLOCKS_TELEGRAM));
                    services.Configure<Services.RequestedLifetimeServiceOptions>(hostContext.Configuration.GetSection(CONST_CONFIGBLOCKS_LIFETIME));
                    services.Configure<Services.DataPopulation.FOCS.FOCSUploadSettings>(hostContext.Configuration.GetSection("focs"));
                    services.Configure<Services.DataPopulation.Demo.DemoUploadSettings>(hostContext.Configuration.GetSection("demo"));

                    services.AddSingleton<Services.RequestedLifetimeService>();
                    services.AddTransient<Services.TelegramService>();
                    services.AddSingleton<Services.DataPopulation.FOCS.ProductCreationService>();

                    services.AddHostedService<Services.DataPopulation.Demo.DataPopulationService>();
                    services.AddHostedService<Services.DataPopulation.FOCS.IntegrationTestsPublisher>();
                    services.AddHostedService<Services.DataPopulation.FOCS.RegressionTestsPublisher>();
                });

            var host = hostBuilder.Build();

            host.Run();

            return 0;
        }
    }
}
