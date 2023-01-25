using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MdChecker;

public record class Hyperlink(string FullPathname, int LineNum, string Url, bool IsWeb)
{
}
