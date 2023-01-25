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

    public Checker(IServiceProvider serviceProvider,
        IOptions<MdCheckerConfiguration> mdCheckerOptions)
    {
        _serviceProvider = serviceProvider;
        if (mdCheckerOptions != null)
        {
            _mdCheckerOptions = mdCheckerOptions.Value;
            RunningTasks = StartWorkers(_mdCheckerOptions.ConcurrencyLevel, _mdCheckerOptions.OneHyperlinkPerThread);
        }
        else
        {
            // used by the tests
            RunningTasks = StartWorkers(1, true);
        }
    }

    public void SignalNoMoreInput() => _noMoreInput = true;
    public Task[] RunningTasks { get; }
    public IList<ValidationResult> Failed => _failed.ToList();

    public async Task EnqueueFilename(string filename)
    {
        if(_mdCheckerOptions.OneHyperlinkPerThread)
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
                    await VerifyHyperlinks(hyperlinks);
                }
                while (!_quit.WaitOne(0));
            }
        }
        finally
        {
            Debug.WriteLine($"{nameof(WorkerPerFile)} is stopping");
            tcs.SetResult();
        }
    }

    private async void WorkerPerLink(object? tcsObject)
    {
        Debug.WriteLine($"{nameof(WorkerPerLink)} is starting on thread {Thread.CurrentThread.ManagedThreadId}");
        ArgumentNullException.ThrowIfNull(tcsObject);
        TaskCompletionSource tcs = (TaskCompletionSource)tcsObject;
        try
        {
            while (!_quit.WaitOne(_timeoutMilliseconds))
            {
                if (_noMoreInput && _inputLinks.Count == 0) return;
                do
                {
                    if (!_inputLinks.TryDequeue(out Hyperlink? hyperlink)) break;
                    Debug.WriteLine($">{hyperlink.Url} - TID:{Thread.CurrentThread.ManagedThreadId}");
                    await VerifyHyperlink(hyperlink);
                }
                while (!_quit.WaitOne(0));
            }
        }
        finally
        {
            Debug.WriteLine($"{nameof(WorkerPerLink)} is stopping");
            tcs.SetResult();
        }
    }

    private Task VerifyHyperlink(Hyperlink hyperlink)
    {
        if (hyperlink.IsWeb) return VerifyWebHyperlink(hyperlink);
        return VerifyFile(hyperlink);
    }

    public async Task VerifyHyperlinks(List<Hyperlink> hyperlinks)
    {
        var taskFiles = VerifyFiles(hyperlinks.Where(d => !d.IsWeb));
        var taskLinks = VerifyWebHyperlinks(hyperlinks.Where(d => d.IsWeb));
        await Task.WhenAll(taskFiles, taskLinks);
    }

    public async Task VerifyWebHyperlinks(IEnumerable<Hyperlink> hyperlinks)
    {
        using var scope = _serviceProvider.CreateScope();
        var client = scope.ServiceProvider.GetRequiredService<CheckerHttpClient>();
        foreach (var hyperlink in hyperlinks)
        {
            Debug.WriteLine($"{nameof(VerifyWebHyperlinks)} {hyperlink.Url}");
            var (success, statusCode, error) = await client.VerifyGet(hyperlink.Url);
            if (!success)
            {
                var validationResult = new ValidationResult(success, statusCode, error, hyperlink);
                _failed.Enqueue(validationResult);
            }
        }
    }

    public async Task VerifyWebHyperlink(Hyperlink hyperlink)
    {
        Debug.WriteLine($"{nameof(VerifyWebHyperlink)} {hyperlink.Url}");
        using var scope = _serviceProvider.CreateScope();
        var client = scope.ServiceProvider.GetRequiredService<CheckerHttpClient>();
        var (success, statusCode, error) = await client.VerifyGet(hyperlink.Url);
        if (!success)
        {
            var validationResult = new ValidationResult(success, statusCode, error, hyperlink);
            _failed.Enqueue(validationResult);
        }
    }

    private Task VerifyFiles(IEnumerable<Hyperlink> hyperlinks)
    {
        foreach (var hyperlink in hyperlinks)
        {
            VerifyFile(hyperlink);
        }

        return Task.CompletedTask;
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
