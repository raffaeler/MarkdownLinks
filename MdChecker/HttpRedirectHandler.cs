using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MdChecker;

public class HttpRedirectHandler : HttpClientHandler
{
    protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        //request.
        return base.Send(request, cancellationToken);
    }
}
