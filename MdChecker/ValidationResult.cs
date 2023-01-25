using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace MdChecker;

public record struct ValidationResult(bool Success, HttpStatusCode? StatusCode, string ErrorMessage, Hyperlink Document)
{
    public string ToReport()
    {
        var code = StatusCode == null ? "-1" : ((int)StatusCode).ToString();
        return $"{code}; {ErrorMessage}; {Document.LineNum}; {Document.Url}; {Document.FullPathname}";
    }
}
