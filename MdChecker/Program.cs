
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
using Polly.Timeout;

namespace MdChecker;

public class Program
{
    static Task<int> Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        CreateHostBuilder(args).Build().Run();
        return Task.FromResult(0);
    }

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
                    c.Timeout = TimeSpan.FromSeconds(30);
                })
                .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
                {
                    MaxConnectionsPerServer = 100,
                    // HttpClient does not support redirects from https to http:
                    // https://github.com/dotnet/runtime/issues/28039
                    AllowAutoRedirect = false,
                })
                .AddPolicyHandler(policy =>
                {
                    return HttpPolicyExtensions
                        .HandleTransientHttpError()
                        .Or<TimeoutRejectedException>()
                        //.OrResult(msg => msg.StatusCode == System.Net.HttpStatusCode.NotFound)
                        //.OrResult(msg => msg.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                        .WaitAndRetryAsync(3, retry => TimeSpan.FromSeconds(1));
                })
                .AddTypedClient<CheckerHttpClient>();

                services.AddHostedService<App>();
            });
    }

}

