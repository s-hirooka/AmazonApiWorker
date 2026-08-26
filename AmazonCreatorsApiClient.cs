using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AmazonApiWorker;

public sealed class AmazonCreatorsApiClient : IDisposable
{
    private readonly HttpClient _httpClient = new();
    private readonly string _credentialId;
    private readonly string _credentialSecret;
    private readonly string _version;
    private readonly string _partnerTag;
    private readonly string _marketplace;
    private string? _accessToken;
    private DateTimeOffset _accessTokenExpiresAtUtc = DateTimeOffset.MinValue;

    public AmazonCreatorsApiClient(
        string credentialId,
        string credentialSecret,
        string version,
        string partnerTag,
        string marketplace)
    {
        _credentialId = credentialId;
        _credentialSecret = credentialSecret;
        _version = version;
        _partnerTag = partnerTag;
        _marketplace = string.IsNullOrWhiteSpace(marketplace) ? "www.amazon.co.jp" : marketplace;
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
    }

    public async Task<(bool Success, string Url, string Error)> GetAffiliateLinkByAsinAsync(
        string asin,
        CancellationToken cancellationToken)
    {
        string token = await GetAccessTokenAsync(cancellationToken).ConfigureAwait(false);
        var body = new GetItemsRequest
        {
            ItemIds = new List<string> { asin },
            PartnerTag = _partnerTag,
            Marketplace = _marketplace,
            Resources = new List<string> { "itemInfo.title", "images.primary.medium" }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://creatorsapi.amazon/catalog/v1/getItems");
        request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        request.Headers.TryAddWithoutValidation("Authorization", IsVersion3() ? $"Bearer {token}" : $"Bearer {token}, Version {_version}");
        request.Headers.TryAddWithoutValidation("x-marketplace", _marketplace);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        string responseText = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            return (false, "", $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}: {responseText}");
        }

        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var apiResponse = JsonSerializer.Deserialize<GetItemsResponse>(responseText, options);
        string url = apiResponse?.ItemsResult?.Items?.FirstOrDefault()?.DetailPageUrl ?? "";

        return string.IsNullOrWhiteSpace(url)
            ? (false, "", "Creators API でURLを取得できませんでした。")
            : (true, url, "");
    }

    private async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(_accessToken) &&
            DateTimeOffset.UtcNow < _accessTokenExpiresAtUtc.AddSeconds(-60))
        {
            return _accessToken;
        }

        bool isVersion3 = IsVersion3();
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            isVersion3 ? GetLwaTokenEndpoint() : "https://creatorsapi.auth.us-west-2.amazoncognito.com/oauth2/token");

        string scope = isVersion3 ? "creatorsapi::default" : "creatorsapi/default";
        string content = $"grant_type=client_credentials&client_id={Uri.EscapeDataString(_credentialId)}&client_secret={Uri.EscapeDataString(_credentialSecret)}&scope={Uri.EscapeDataString(scope)}";
        request.Content = new StringContent(content, Encoding.UTF8, "application/x-www-form-urlencoded");

        if (!isVersion3)
        {
            string basic = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_credentialId}:{_credentialSecret}"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", basic);
        }

        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        string responseText = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"トークン取得失敗: HTTP {(int)response.StatusCode} {response.ReasonPhrase}: {responseText}");
        }

        var token = JsonSerializer.Deserialize<TokenResponse>(
            responseText,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (string.IsNullOrWhiteSpace(token?.AccessToken))
        {
            throw new InvalidOperationException("トークン取得レスポンスが不正です。");
        }

        _accessToken = token.AccessToken;
        _accessTokenExpiresAtUtc = DateTimeOffset.UtcNow.AddSeconds(token.ExpiresIn <= 0 ? 3600 : token.ExpiresIn);
        return _accessToken;
    }

    private bool IsVersion3()
    {
        return (_version ?? "").Trim().StartsWith("3.", StringComparison.OrdinalIgnoreCase);
    }

    private string GetLwaTokenEndpoint()
    {
        string version = (_version ?? "").Trim();
        if (version.StartsWith("3.1", StringComparison.OrdinalIgnoreCase))
        {
            return "https://api.amazon.com/auth/o2/token";
        }

        if (version.StartsWith("3.2", StringComparison.OrdinalIgnoreCase))
        {
            return "https://api.amazon.co.uk/auth/o2/token";
        }

        return "https://api.amazon.co.jp/auth/o2/token";
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }

    private sealed class TokenResponse
    {
        [JsonPropertyName("access_token")]
        public string? AccessToken { get; set; }

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }
    }

    private sealed class GetItemsRequest
    {
        [JsonPropertyName("itemIds")]
        public List<string> ItemIds { get; set; } = new();

        [JsonPropertyName("partnerTag")]
        public string PartnerTag { get; set; } = "";

        [JsonPropertyName("marketplace")]
        public string Marketplace { get; set; } = "";

        [JsonPropertyName("resources")]
        public List<string> Resources { get; set; } = new();
    }

    private sealed class GetItemsResponse
    {
        [JsonPropertyName("itemsResult")]
        public ItemsResult? ItemsResult { get; set; }
    }

    private sealed class ItemsResult
    {
        [JsonPropertyName("items")]
        public List<Item>? Items { get; set; }
    }

    private sealed class Item
    {
        [JsonPropertyName("detailPageUrl")]
        public string? DetailPageUrl { get; set; }
    }
}
