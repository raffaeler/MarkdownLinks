using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;

namespace MdChecker;

public class CheckerHttpClient
{
    public CheckerHttpClient(HttpClient client)
    {
        this.Client = client;
    }

    private HttpClient Client { get; }

    public async Task<(bool success, HttpStatusCode? statusCode, string error)> VerifyGet(string address)
    {
        try
        {
            Uri uri = new Uri(address);
            var ipHost = await Dns.GetHostEntryAsync(uri.DnsSafeHost);
            if(ipHost == null || ipHost.AddressList.Length == 0)
            {
                return (false, null, $"Invalid host name: {uri.DnsSafeHost} ({address})");
            }

            var ip = ipHost.AddressList.First();
            using var response = await Client.GetAsync(address);
            if ((int)response.StatusCode >= 300 && (int)response.StatusCode <= 399)
            {
                var redirectUri = response.Headers.Location;
                var requestUri = response.RequestMessage?.RequestUri;
                Debug.WriteLine($"Location {redirectUri}");
                if (redirectUri != null && !redirectUri.IsAbsoluteUri && requestUri != null)
                {
                    var authority = requestUri.GetLeftPart(UriPartial.Authority);
                    var resource = redirectUri.ToString();
                    string path;
                    if (resource.StartsWith("/"))
                    {
                        path = string.Empty;
                    }
                    else
                    {
                        path = string.Join("", requestUri.Segments.Take(requestUri.Segments.Length - 1));
                    }

                    redirectUri = new Uri(string.Join("", authority, path, resource), UriKind.Absolute);

                    if (redirectUri == response.Headers.Location)
                    {
                        throw new Exception($"Redirection loop detected for {response.Headers.Location}");
                    }
                }

                Debug.WriteLine($"Redirect: {address} => {redirectUri}");
                return await VerifyGet(redirectUri!.ToString());
            }

            return (response.IsSuccessStatusCode, response.StatusCode, string.Empty);
        }
        catch (HttpRequestException err)
        {
            Debug.WriteLine($"Failed with status {err.StatusCode}");
            return (false, null, $"HttpRequestException - address: {address}");
        }
        catch (Exception err)
        {
            return (false, null, $"{err.Message} - address: {address}");
        }
    }
}
