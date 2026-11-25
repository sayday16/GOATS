using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using Serilog;

namespace LogWindowUI.Services
{
    public class BearerTokenHandler : DelegatingHandler
    {
        public BearerTokenHandler() : base(new HttpClientHandler())
        {
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (AuthSession.IsActive)
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", AuthSession.AccessToken);

            var path = request.RequestUri?.AbsolutePath;
            var stopwatch = Stopwatch.StartNew();

            try
            {
                var response = await base.SendAsync(request, cancellationToken);

                if (response.IsSuccessStatusCode)
                    Log.Information("API {Method} {Path} responded {Status} in {Elapsed} ms", request.Method, path, (int)response.StatusCode, stopwatch.ElapsedMilliseconds);
                else
                    Log.Warning("API {Method} {Path} responded {Status} in {Elapsed} ms", request.Method, path, (int)response.StatusCode, stopwatch.ElapsedMilliseconds);

                return response;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Log.Error(ex, "API {Method} {Path} failed after {Elapsed} ms", request.Method, path, stopwatch.ElapsedMilliseconds);
                throw;
            }
        }
    }
}
