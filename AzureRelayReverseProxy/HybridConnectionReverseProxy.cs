using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Relay;

namespace AzureRelayReverseProxy
{
    class HybridConnectionReverseProxy
    {
        readonly HybridConnectionListener listener;
        readonly HttpClient httpClient;
        readonly string hybridConnectionSubpath;

        public HybridConnectionReverseProxy(string connectionString, Uri targetUri)
        {
            listener = new HybridConnectionListener(connectionString);
            httpClient = new HttpClient
            {
                BaseAddress = targetUri
            };
            httpClient.DefaultRequestHeaders.ExpectContinue = false;
            hybridConnectionSubpath = listener.Address.AbsolutePath.EnsureEndsWith("/");
        }

        public async Task OpenAsync(CancellationToken cancelToken)
        {
            listener.RequestHandler = async (context) => await RequestHandler(context);
            await listener.OpenAsync(cancelToken);
            Console.WriteLine($"Forwarding from {listener.Address} to {httpClient.BaseAddress}.");
        }

        public Task CloseAsync(CancellationToken cancelToken)
        {
            return listener.CloseAsync(cancelToken);
        }

        async Task RequestHandler(RelayedHttpListenerContext context)
        {
            DateTime startTimeUtc = DateTime.UtcNow;
            try
            {
                HttpRequestMessage requestMessage = CreateHttpRequestMessage(context);
                HttpResponseMessage responseMessage = await httpClient.SendAsync(requestMessage);
                await SendResponseAsync(context, responseMessage);
                await context.Response.CloseAsync();
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error: {e.GetType().Name}: {e.Message}");
                SendErrorResponse(e, context);
            }
            finally
            {
                LogRequest(startTimeUtc, context);
            }
        }

        static async Task SendResponseAsync(RelayedHttpListenerContext context, HttpResponseMessage responseMessage)
        {
            context.Response.StatusCode = responseMessage.StatusCode;
            context.Response.StatusDescription = responseMessage.ReasonPhrase;
            foreach (KeyValuePair<string, IEnumerable<string>> header in responseMessage.Headers)
            {
                if (string.Equals(header.Key, "Transfer-Encoding"))
                {
                    continue;
                }

                context.Response.Headers.Add(header.Key, string.Join(",", header.Value));
            }

            var responseStream = await responseMessage.Content.ReadAsStreamAsync();
            await responseStream.CopyToAsync(context.Response.OutputStream);
        }

        static void SendErrorResponse(Exception e, RelayedHttpListenerContext context)
        {
            context.Response.StatusCode = HttpStatusCode.InternalServerError;
            context.Response.StatusDescription = e.Message;
            context.Response.Close();
        }

        HttpRequestMessage CreateHttpRequestMessage(RelayedHttpListenerContext context)
        {
            var requestMessage = new HttpRequestMessage();
            if (context.Request.HasEntityBody)
            {
                requestMessage.Content = new StreamContent(context.Request.InputStream);
                string contentType = context.Request.Headers[HttpRequestHeader.ContentType];
                if (!string.IsNullOrEmpty(contentType))
                {
                    requestMessage.Content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
                }
            }

            string relativePath = context.Request.Url.GetComponents(UriComponents.PathAndQuery, UriFormat.Unescaped);
            relativePath = relativePath.Replace(this.hybridConnectionSubpath, string.Empty, StringComparison.OrdinalIgnoreCase);
            requestMessage.RequestUri = new Uri(relativePath, UriKind.RelativeOrAbsolute);
            requestMessage.Method = new HttpMethod(context.Request.HttpMethod);

            foreach (var headerName in context.Request.Headers.AllKeys)
            {
                if (string.Equals(headerName, "Host", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(headerName, "Content-Type", StringComparison.OrdinalIgnoreCase))
                {
                    // Don't flow these headers here
                    continue;
                }

                requestMessage.Headers.Add(headerName, context.Request.Headers[headerName]);
            }

            return requestMessage;
        }

        static void LogRequest(DateTime startTimeUtc, RelayedHttpListenerContext context)
        {
            DateTime stopTimeUtc = DateTime.UtcNow;
            StringBuilder buffer = new();
            buffer.Append($"{startTimeUtc.ToString("s", CultureInfo.InvariantCulture)}, ");
            buffer.Append($"\"{context.Request.HttpMethod} {context.Request.Url.GetComponents(UriComponents.PathAndQuery, UriFormat.Unescaped)}\", ");
            buffer.Append($"{(int)context.Response.StatusCode}, ");
            buffer.Append($"{(int)stopTimeUtc.Subtract(startTimeUtc).TotalMilliseconds}");
            Console.WriteLine(buffer);
        }
    }
}
