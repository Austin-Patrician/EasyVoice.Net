namespace EasyVoice.Core.Models;

public class OpenAIHttpClientHandler(string? requestUrl = null): HttpClientHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(requestUrl))
        {
            request.RequestUri = new Uri(requestUrl);
        }
        
        return await base.SendAsync(request, cancellationToken);
    }
}