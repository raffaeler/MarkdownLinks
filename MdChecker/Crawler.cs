using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MdChecker;

public class Crawler
{
    private readonly ILogger<Crawler> _logger;
    private readonly MdCheckerConfiguration _mdCheckerOptions;
    private string _searchPattern;

    public Crawler(ILogger<Crawler> logger,
        IOptions<MdCheckerConfiguration> mdCheckerOptions)
    {
        _logger = logger;
        _mdCheckerOptions = mdCheckerOptions.Value ?? new MdCheckerConfiguration();
        _searchPattern = $"*.{_mdCheckerOptions.MarkdownExtension.Trim('.')}";
    }

    public async Task<(int, int)> WalkTree(Func<string, Task> onFile)
    {
        int processedCounter = 0;
        int excludedCounter = 0;
        var root = Path.GetFullPath(_mdCheckerOptions.RootPath);
        var allFiles = Directory.EnumerateFiles(root, _searchPattern, SearchOption.AllDirectories);
        foreach (var file in allFiles)
        {
            FileInfo fi = new(file);
            if (fi.DirectoryName == null)
            {
                _logger.LogError($"There is no DirectoryName for {file}");
                continue;
            }

            bool isExcluded = false;
            foreach (var excluded in _mdCheckerOptions.ExcludedRelativePaths)
            {
                if (DirectoryEquals(excluded, fi.DirectoryName))
                {
                    isExcluded = true;
                }
            }

            if (!isExcluded)
            {
                await onFile(file);
                processedCounter++;
            }
            else
            {
                excludedCounter++;
            }
        }

        return (processedCounter, excludedCounter);
    }

    public bool DirectoryEquals(string relative, string fullPath)
    {
        var left = Path.GetFullPath(relative).TrimEnd('\\');
        var right = Path.GetFullPath(fullPath).TrimEnd('\\');
        return string.Compare(left, right, StringComparison.InvariantCultureIgnoreCase) == 0;
    }
}
