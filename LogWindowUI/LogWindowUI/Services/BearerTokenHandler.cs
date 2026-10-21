using System.Net.Http;
using System.Net.Http.Headers;

namespace LogWindowUI.Services
{
    public class BearerTokenHandler : DelegatingHandler
    {
        public BearerTokenHandler() : base(new HttpClientHandler())
        {
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (AuthSession.IsActive)
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", AuthSession.AccessToken);

            return base.SendAsync(request, cancellationToken);
        }
    }
}
