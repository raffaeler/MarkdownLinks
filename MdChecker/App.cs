using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MdChecker;

public class App : BackgroundService
{
    private readonly ILogger<App> _logger;
    private readonly IHostApplicationLifetime _hostApplicationLifetime;
    private readonly Crawler _crawler;
    private readonly Checker _checker;
    private readonly MdCheckerConfiguration _mdCheckerOptions;

    public App(ILogger<App> logger,
        IHostApplicationLifetime hostApplicationLifetime,
        IOptions<MdCheckerConfiguration> mdCheckerOptions,
        Crawler crawler,
        Checker checker)
    {
        _logger = logger;
        _hostApplicationLifetime = hostApplicationLifetime;
        _crawler = crawler;
        _checker = checker;
        _mdCheckerOptions = mdCheckerOptions.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Stopwatch sw = new();
        sw.Start();
        (int processed, int excluded) = await _crawler.WalkTree(_checker.EnqueueFilename);

        _checker.SignalNoMoreInput();
        await Task.WhenAll(_checker.RunningTasks);
        Debug.WriteLine("tasks have finished");
        sw.Stop();

        var report = CreateReport(sw.Elapsed, processed, excluded);
        await File.WriteAllTextAsync("MdCheckerReport.txt", report);
        Debug.WriteLine(report);
        _hostApplicationLifetime.StopApplication();
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        var baseres = base.StopAsync(cancellationToken);
        _hostApplicationLifetime.StopApplication();
        return baseres;
    }

    private string CreateReport(TimeSpan elapsed, int processed, int excluded)
    {
        var main = string.Join(Environment.NewLine, _checker.Failed
            .Select(f => $"{f.ToReport()}"));

        var timing = $"Total Time: {elapsed.TotalSeconds}s";
        string files = $"Files - Processed:{processed}; Excluded:{excluded}";
        return string.Join(Environment.NewLine, main, timing, files) + Environment.NewLine;
    }
}
