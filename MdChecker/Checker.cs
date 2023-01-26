using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Markdig;
using Markdig.Extensions.Figures;
using Markdig.Parsers;
using Markdig.Renderers;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace MdChecker;

public class Checker
{
    private readonly IServiceProvider _serviceProvider;
    private readonly MdCheckerConfiguration _mdCheckerOptions;
    private ManualResetEvent _quit = new ManualResetEvent(false);
    private int _timeoutMilliseconds = 500;
    private bool _noMoreInput;

    private ConcurrentQueue<string> _inputFiles = new();
    private ConcurrentQueue<Hyperlink> _inputLinks = new();
    private ConcurrentQueue<ValidationResult> _failed = new();
    private List<Thread> _threads = new();
    private object _resultsLock = new();
    private int _successes;
    private int _failures;

    public Checker(IServiceProvider serviceProvider,
        IOptions<MdCheckerConfiguration> mdCheckerOptions)
    {
        _serviceProvider = serviceProvider;
        _mdCheckerOptions = mdCheckerOptions?.Value ?? new MdCheckerConfiguration();
        RunningTasks = StartWorkers(_mdCheckerOptions.ConcurrencyLevel, _mdCheckerOptions.OneHyperlinkPerThread);
    }

    public void SignalNoMoreInput() => _noMoreInput = true;
    public Task[] RunningTasks { get; }
    public IList<ValidationResult> Failed => _failed.ToList();

    public int Successes
    {
        get { lock (_resultsLock) { return _successes; } }
    }

    public int Failures
    {
        get { lock (_resultsLock) { return _failures; } }
    }

    public async Task EnqueueFilename(string filename)
    {
        if (_mdCheckerOptions.OneHyperlinkPerThread)
        {
            //await Task.Run(async () =>
            //{
            var hyperlinks = await CheckFileAsync(filename);
            foreach (var hyperlink in hyperlinks)
            {
                Debug.WriteLine("Enqueuing hyperlink");
                _inputLinks.Enqueue(hyperlink);
            }
            //});
        }
        else
        {
            Debug.WriteLine("Enqueuing filename");
            _inputFiles.Enqueue(filename);
        }
    }

    private Task[] StartWorkers(int concurrencyLevel, bool isOneHyperlinkPerThread)
    {
        //Parallel.For(0, concurrencyLevel, Worker);
        List<Task> tasks = new();
        _threads = new();
        ParameterizedThreadStart worker = isOneHyperlinkPerThread
            ? WorkerPerLink
            : WorkerPerFile;

        for (int i = 0; i < concurrencyLevel; i++)
        {
            TaskCompletionSource tcs = new TaskCompletionSource();
            var thread = new Thread(worker);
            _threads.Add(thread);
            tasks.Add(tcs.Task);
            thread.IsBackground = true;
            thread.Priority = ThreadPriority.Normal;
            thread.Start(tcs);
        }

        return tasks.ToArray();
    }

    private async void WorkerPerFile(object? tcsObject)
    {
        Debug.WriteLine($"{nameof(WorkerPerFile)} is starting on thread {Thread.CurrentThread.ManagedThreadId}");
        ArgumentNullException.ThrowIfNull(tcsObject);
        TaskCompletionSource tcs = (TaskCompletionSource)tcsObject;
        int successes = 0;
        int failures = 0;
        try
        {
            while (!_quit.WaitOne(_timeoutMilliseconds))
            {
                if (_noMoreInput && _inputFiles.Count == 0) return;

                do
                {
                    if (!_inputFiles.TryDequeue(out string? filename)) break;
                    var hyperlinks = await CheckFileAsync(filename);
                    Debug.WriteLine($">{filename}:{hyperlinks.Count} - TID:{Thread.CurrentThread.ManagedThreadId}");
                    var (success, failure) = await VerifyHyperlinks(hyperlinks);
                    successes += success;
                    failures += failure;
                }
                while (!_quit.WaitOne(0));
            }
        }
        finally
        {
            Debug.WriteLine($"{nameof(WorkerPerFile)} is stopping");
            tcs.SetResult();
            lock (_resultsLock)
            {
                _successes += successes;
                _failures += failures;
            }
        }

    }

    private async void WorkerPerLink(object? tcsObject)
    {
        Debug.WriteLine($"{nameof(WorkerPerLink)} is starting on thread {Thread.CurrentThread.ManagedThreadId}");
        ArgumentNullException.ThrowIfNull(tcsObject);
        TaskCompletionSource tcs = (TaskCompletionSource)tcsObject;
        int successes = 0;
        int failures = 0;
        try
        {
            while (!_quit.WaitOne(_timeoutMilliseconds))
            {
                if (_noMoreInput && _inputLinks.Count == 0) return;
                do
                {
                    if (!_inputLinks.TryDequeue(out Hyperlink? hyperlink)) break;
                    Debug.WriteLine($">{hyperlink.Url} - TID:{Thread.CurrentThread.ManagedThreadId}");
                    var (success, failure) = await VerifyHyperlink(hyperlink);
                    successes += success;
                    failures += failure;
                }
                while (!_quit.WaitOne(0));
            }
        }
        finally
        {
            Debug.WriteLine($"{nameof(WorkerPerLink)} is stopping");
            tcs.SetResult();
            lock (_resultsLock)
            {
                _successes += successes;
                _failures += failures;
            }
        }
    }

    private Task<(int success, int fail)> VerifyHyperlink(Hyperlink hyperlink)
    {
        if (hyperlink.IsWeb) return VerifyWebHyperlink(hyperlink);
        return VerifyFile(hyperlink);
    }

    public async Task<(int success, int fail)> VerifyHyperlinks(List<Hyperlink> hyperlinks)
    {
        var taskFiles = VerifyFiles(hyperlinks.Where(d => !d.IsWeb));
        var taskLinks = VerifyWebHyperlinks(hyperlinks.Where(d => d.IsWeb));
        await Task.WhenAll(taskFiles, taskLinks);
        var successes = taskFiles.Result.success + taskLinks.Result.success;
        var failures = taskFiles.Result.fail + taskLinks.Result.fail;
        return (successes, failures);
    }

    public async Task<(int success, int fail)> VerifyWebHyperlinks(IEnumerable<Hyperlink> hyperlinks)
    {
        var successes = 0;
        var failures = 0;
        using var scope = _serviceProvider.CreateScope();
        var client = scope.ServiceProvider.GetRequiredService<CheckerHttpClient>();
        foreach (var hyperlink in hyperlinks)
        {
            Debug.WriteLine($"{nameof(VerifyWebHyperlinks)} {hyperlink.Url}");
            var (success, statusCode, error) = await client.VerifyResource(hyperlink.Url);
            if (!success)
            {
                var validationResult = new ValidationResult(success, statusCode, error, hyperlink);
                _failed.Enqueue(validationResult);
                failures++;
            }
            else
            {
                successes++;
            }

        }

        return (successes, failures);
    }

    public async Task<(int success, int fail)> VerifyWebHyperlink(Hyperlink hyperlink)
    {
        var successes = 0;
        var failures = 0;
        Debug.WriteLine($"{nameof(VerifyWebHyperlink)} {hyperlink.Url}");
        using var scope = _serviceProvider.CreateScope();
        var client = scope.ServiceProvider.GetRequiredService<CheckerHttpClient>();
        var (success, statusCode, error) = await client.VerifyResource(hyperlink.Url);
        if (!success)
        {
            var validationResult = new ValidationResult(success, statusCode, error, hyperlink);
            _failed.Enqueue(validationResult);
            failures++;
        }
        else
        {
            successes++;
        }

        return (successes, failures);
    }

    private async Task<(int success, int fail)> VerifyFiles(IEnumerable<Hyperlink> hyperlinks)
    {
        int successes = 0;
        int failures = 0;
        foreach (var hyperlink in hyperlinks)
        {
            var (success, failure) = await VerifyFile(hyperlink);
            successes += success;
            failures += failure;
        }

        return (successes, failures);
    }

    private Task<(int success, int fail)> VerifyFile(Hyperlink hyperlink)
    {
        Debug.WriteLine($"{nameof(VerifyFile)} {hyperlink.Url}");
        if (!File.Exists(hyperlink.FullPathname))
        {
            var validationResult = new ValidationResult(false, null, "File does not exists", hyperlink);
            _failed.Enqueue(validationResult);
            return Task.FromResult((0, 1));
        }

        return Task.FromResult((1, 0));
    }

    public async Task<List<Hyperlink>> CheckFileAsync(string filename)
    {
        string? fullPathname = null;
        try
        {
            fullPathname = Path.GetFullPath(filename);
            var md = await File.ReadAllTextAsync(fullPathname);
            return CheckMarkdownString(fullPathname, md);
        }
        catch (Exception err)
        {
            Console.WriteLine(err);
            return new List<Hyperlink>()
            {
                new Hyperlink(fullPathname ?? filename, 0, string.Empty, false)
            };
        }
    }

    public List<Hyperlink> CheckMarkdownString(string fullPathname, string markdown)
    {
        try
        {
            var pipeline = new MarkdownPipelineBuilder()
                .UseAdvancedExtensions()
                .Build();
            var document = Markdown.Parse(markdown, pipeline);

            var links = document
                .Descendants<LinkInline>()
                .Select(d => new Hyperlink(fullPathname, d.Line, d.Url ?? string.Empty, IsWeb(d.Url)))
                .ToList();

            return links ?? new List<Hyperlink>();
        }
        catch (Exception err)
        {
            Console.WriteLine(err);
            return new List<Hyperlink>()
            {
                new Hyperlink(fullPathname, 0, string.Empty, false)
            };
        }
    }

    private bool IsWeb(string? link)
        => link != null && (link.StartsWith("https://") || link.StartsWith("http://"));
}
