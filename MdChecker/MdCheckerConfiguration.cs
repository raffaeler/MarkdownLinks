using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MdChecker;

public class MdCheckerConfiguration
{
    public bool OneHyperlinkPerThread { get; set; } = true;
    public int ConcurrencyLevel { get; set; }
    public string RootPath { get; set; } = @".";
    public string MarkdownExtension { get; set; } = "md";
    public List<string> ExcludedRelativePaths { get; set; } = new List<string>();
}
