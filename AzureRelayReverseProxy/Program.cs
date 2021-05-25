using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace AzureRelayReverseProxy
{
    class Program
    {
        static readonly List<Proxy> proxies = new();

        static void Main(string[] args)
        {
            using IHost host = CreateHostBuilder(args).Build();

            Console.WriteLine("Azure Relay Reverse Proxy");
            Console.WriteLine($"{proxies.Count} proxy configurations loaded");

            if (proxies.Count > 0)
            {
                List<Task> proxyTasks = new();

                foreach (var proxy in proxies)
                {
                    Uri targetUri = new(proxy.TargetUri.EnsureEndsWith("/"));
                    proxyTasks.Add(StartProxy(proxy.ConnectionString, targetUri));
                }

                Task.WhenAll(proxyTasks).GetAwaiter().GetResult();
            }

            Console.WriteLine("Shutting down client");
        }

        static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureAppConfiguration((hostingContext, configuration) =>
                {
                    configuration.Sources.Clear();

                    IHostEnvironment env = hostingContext.HostingEnvironment;

                    configuration
                        .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                        .AddJsonFile($"appsettings.{env.EnvironmentName}.json", true, true);

                    IConfigurationRoot configurationRoot = configuration.Build();
                    configurationRoot.GetSection("Proxies").Bind(proxies);
                });

        static async Task StartProxy(string connectionString, Uri targetUri)
        {
            HybridConnectionReverseProxy hybridProxy = new(connectionString, targetUri);
            await hybridProxy.OpenAsync(CancellationToken.None);
            Console.ReadLine();
            await hybridProxy.CloseAsync(CancellationToken.None);
        }
    }
}
