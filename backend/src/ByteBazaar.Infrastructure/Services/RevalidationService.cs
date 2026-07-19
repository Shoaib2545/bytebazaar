using System.Net.Http.Json;
using ByteBazaar.Application.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ByteBazaar.Infrastructure.Services;

/// <summary>
/// Fire-and-forget POSTs to the storefront's /api/revalidate route so ISR pages refresh after
/// catalog/stock changes. Failures are logged as warnings only — never propagated.
/// </summary>
public class RevalidationService : IStorefrontRevalidator
{
    public const string HttpClientName = "storefront-revalidate";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<RevalidationService> _logger;

    public RevalidationService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<RevalidationService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public void Revalidate(params string[] paths)
    {
        var baseUrl = _configuration["Storefront:BaseUrl"];
        var secret = _configuration["Storefront:RevalidateSecret"];
        if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(secret) || paths.Length == 0)
            return;

        var endpoint = $"{baseUrl.TrimEnd('/')}/api/revalidate";

        _ = Task.Run(async () =>
        {
            foreach (var path in paths)
            {
                try
                {
                    var client = _httpClientFactory.CreateClient(HttpClientName);
                    var response = await client.PostAsJsonAsync(endpoint, new { path, secret });
                    if (!response.IsSuccessStatusCode)
                        _logger.LogWarning("Storefront revalidation for {Path} returned {StatusCode}.", path, (int)response.StatusCode);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Storefront revalidation for {Path} failed.", path);
                }
            }
        });
    }
}
