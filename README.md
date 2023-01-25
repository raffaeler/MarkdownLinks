# MarkdownLinks
This project crawls a directory tree and looks for broken links in [markdown files](https://www.markdownguide.org).

The project is composed of two C# projects using .NET:

- `MdChecker`. The console application doing the hard job
- `MdChecker.Tests`. The test project.

> Currently there are not enough tests, but I could successfully validate the outcome in a complex directory structure.

## How does it work

The console application is intended to be run unattended on a server, possibly from a YAML CI build step. For this reason, it does not take any keyboard input and only prints the final report on the console at the very end.

The output is generated as the file `Report.txt`, one for the entire directory tree.

The tool has a few goals:

- Process the files and links as fast as possible, using multiple threads to concurrently process the requests
  - The concurrency level is configurable
- Create a single report for the entire directory tree. This is needed to discover all the broken links at once.
- Process the GET requests for the HTTP/HTTPS URLs with a retry policy whenever the error could be temporary (maybe due to a slow website).

## Configuration

The file `appsettings.json` contains the settings for the crawler and checker:

```
{
    "MdChecker": {
        "OneHyperlinkPerThread": true,
        "ConcurrencyLevel": 12,
        "RootPath": "H:/Samples/_forks/iot",
        "MarkdownExtension": "md",
        "ExcludedRelativePaths": [
            "bin"
        ]
    }
}
```

- `MdChecker` is the configuration section
- `OneHyperlinkPerThread`. The tool support two strategies explained in the next section.
- `ConcurrencyLevel`. This determines the number of threads used to concurrently process the files and links.
- `RootPath`. The root of the directory tree to process.
- `MarkdownExtension`. The file extension to check for. By default, Markdown files use the "md" extension.
- `ExcludedRelativePaths`. A list of relative paths that should be excluded from the crawler. This option will be expanded in the future.

## Concurrency strategy

The tool supports two concurrency strategies. At the beginning the tool creates a number of threads as specified in the `ConcurrencyLevel` parameter. The behavior of these working threads depends on the `OneHyperlinkPerThread` Boolean parameter.

- `True`. Every thread process a queue of links coming from one or more files.
- `False`. Every thread process a queue of files. Every time a filename is dequeued in a thread, all the links inside the markdown files will be processed in that thread.

The concurrency level can be quite high without drowning the CPU at all. In fact most the time is spent in the network handshake to verify (using the HTTP GET verb) the link is still healthy. The CPU just spend time in analyzing the markdown files, but this work is minimal.

Anyway, setting the concurrency level to a very large number (such as `50`) will result in a waste of time spent from the operating system in fragmenting the execution and will result in a slower overall performance. There is no exact time, you can tune the parameters on your need.

## Networking strategy

The tool makes use of `Polly` which extends the standard `HttpClient` to support retry policies. This is needed to avoid signaling broken links for slow websites.

The `HttpClient` is configured for:

- Validating the DNS name before the `HttpClient` request to avoid wasting time.
- Accepting any media type (`*/*`).
- Disable the `Keep-Alive` to save time.
- Never get the HTTP body. We just need the status code.
- Using the `GET` verb instead of `HEAD` because many websites do not honor `HEAD`.
- Disable the `Cache` because we need to see whether the resource is still there.
- Disable the `AllowAutoRedirect` because the `HttpClient` [does **not** support](https://github.com/dotnet/runtime/issues/28039) redirecting from `HTTPs` to `HTTP`. Redirection is implemented manually in the tool and was tested against some links used by the `dotnet/IoT` repository. Redirection also checks for infinite loops whenever the Location header should point to itself.

### Markdown processing

Markdown links are extracted using the parser of the [Markdig](https://www.nuget.org/packages/Markdig) package.

## TODO list

- Implement the directory exclusions using the regular expressions,
- Implement the URL exclusions using the regular expressions,

