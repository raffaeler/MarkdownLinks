
using System;
using System.Text;
using System.Net;
using System.Net.Http.Headers;

using Markdig;
using Markdig.Parsers;
using Markdig.Renderers;
using Markdig.Renderers.Html.Inlines;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Configuration;

using Polly.Extensions.Http;
using Polly;

namespace MdChecker;

public class Program
{
    static async Task Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        //var p = new Program();
        //var serviceProvider = await p.Start();
        //serviceProvider.Dispose();
        CreateHostBuilder(args).Build().Run();
    }

    //private async Task<ServiceProvider> Start()
    //{
    //    var serviceProvider = Initialize();
    //    var checker = new Checker(serviceProvider);
    //    var results = await checker.CheckFileAsync(@"\\fs\users\ATD\Hardware\Arduino\_esp32\esp32-wroom32E_pins.md");
    //    return serviceProvider;
    //}

    //private ServiceProvider Initialize()
    //{
    //    var services = new ServiceCollection();
    //    services.AddHttpClient("stress-client", c =>
    //    {

    //        c.DefaultRequestHeaders.Add("User-Agent", "MdChecker Http Client");
    //        c.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*"));
    //        c.DefaultRequestHeaders.CacheControl = new CacheControlHeaderValue
    //        {
    //            NoCache = true,
    //            NoStore = true,
    //            MaxAge = new TimeSpan(0),
    //            MustRevalidate = true
    //        };
    //    })
    //    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
    //    {
    //        MaxConnectionsPerServer = 100,
    //    })
    //    .AddPolicyHandler(policy =>
    //    {
    //        return HttpPolicyExtensions
    //            .HandleTransientHttpError()
    //            .OrResult(msg => msg.StatusCode == System.Net.HttpStatusCode.NotFound)
    //            .OrResult(msg => msg.StatusCode == System.Net.HttpStatusCode.Unauthorized)
    //            .WaitAndRetryAsync(3, retry => TimeSpan.FromSeconds(Math.Pow(2, retry)));
    //    })
    //    .AddTypedClient<CheckerHttpClient>();

    //    return services.BuildServiceProvider();
    //}

    public static IHostBuilder CreateHostBuilder(string[] args)
    {
        return Host.CreateDefaultBuilder(args)

            .UseConsoleLifetime()   // Ctrl-C

            .ConfigureLogging(options =>
            {
                // Microsoft.Extensions.Logging
                options.ClearProviders();
                options.AddDebug();
            })

            .ConfigureAppConfiguration(config =>
            {
            })

            .ConfigureServices((hostContext, services) =>
            {
                services.Configure<MdCheckerConfiguration>(hostContext.Configuration.GetSection("MdChecker"));

                services.AddSingleton<Crawler>();
                services.AddSingleton<Checker>();

                services.AddHttpClient("MdChecker-Client", c =>
                {
                    c.DefaultRequestHeaders.Add("User-Agent", "MdChecker Http Client");
                    c.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*"));
                    c.DefaultRequestHeaders.ConnectionClose = true;
                    c.DefaultRequestHeaders.CacheControl = new CacheControlHeaderValue
                    {
                        NoCache = true,
                        NoStore = true,
                        MaxAge = new TimeSpan(0),
                        MustRevalidate = true
                    };
                })
                .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
                {
                    MaxConnectionsPerServer = 100,
                    // HttpClient does not support redirects from https to http
                    AllowAutoRedirect = false,
                })
                .AddPolicyHandler(policy =>
                {
                    return HttpPolicyExtensions
                        .HandleTransientHttpError()
                        //.OrResult(msg => msg.StatusCode == System.Net.HttpStatusCode.NotFound)
                        //.OrResult(msg => msg.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                        .WaitAndRetryAsync(3, retry => TimeSpan.FromSeconds(1));
                })
                .AddTypedClient<CheckerHttpClient>();

                services.AddHostedService<App>();
            });
    }

}

