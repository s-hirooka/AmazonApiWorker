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

    // リンク作成に最低限必要な項目（従来から要求していたもの）
    private static readonly string[] MinimalResources =
    {
        "itemInfo.title",
        "images.primary.medium"
    };

    // 取得できる商品情報をすべて要求する。結果は twi-autoV2 へそのまま渡す。
    private static readonly string[] FullResources =
    {
        "browseNodeInfo.browseNodes",
        "browseNodeInfo.browseNodes.ancestor",
        "browseNodeInfo.browseNodes.salesRank",
        "browseNodeInfo.websiteSalesRank",
        "customerReviews.count",
        "customerReviews.starRating",
        "images.primary.small",
        "images.primary.medium",
        "images.primary.large",
        "images.variants.small",
        "images.variants.medium",
        "images.variants.large",
        "itemInfo.byLineInfo",
        "itemInfo.contentInfo",
        "itemInfo.contentRating",
        "itemInfo.classifications",
        "itemInfo.externalIds",
        "itemInfo.features",
        "itemInfo.manufactureInfo",
        "itemInfo.productInfo",
        "itemInfo.technicalInfo",
        "itemInfo.title",
        "itemInfo.tradeInInfo",
        "offersV2.listings.availability",
        "offersV2.listings.condition",
        "offersV2.listings.dealDetails",
        "offersV2.listings.isBuyBoxWinner",
        "offersV2.listings.loyaltyPoints",
        "offersV2.listings.merchantInfo",
        "offersV2.listings.price",
        "offersV2.listings.type",
        "parentASIN"
    };

    /// <summary>
    /// ASIN からアフィリエイトリンクと商品情報を取得する。
    /// ItemJson は API が返した商品1件分のJSON（取得できなかったときは空文字）。
    /// 全項目の要求が失敗した場合は、従来と同じ最小限の項目で取り直す（リンク作成を止めないため）。
    /// </summary>
    public async Task<(bool Success, string Url, string Error, string ItemJson)> GetAffiliateLinkByAsinAsync(
        string asin,
        CancellationToken cancellationToken)
    {
        var full = await GetItemAsync(asin, FullResources, cancellationToken).ConfigureAwait(false);
        if (full.Success)
        {
            return full;
        }

        var minimal = await GetItemAsync(asin, MinimalResources, cancellationToken).ConfigureAwait(false);
        return minimal.Success ? minimal : full;
    }

    private async Task<(bool Success, string Url, string Error, string ItemJson)> GetItemAsync(
        string asin,
        IEnumerable<string> resources,
        CancellationToken cancellationToken)
    {
        string token = await GetAccessTokenAsync(cancellationToken).ConfigureAwait(false);
        var body = new GetItemsRequest
        {
            ItemIds = new List<string> { asin },
            PartnerTag = _partnerTag,
            Marketplace = _marketplace,
            Resources = resources.ToList()
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
            return (false, "", $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}: {responseText}", "");
        }

        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var apiResponse = JsonSerializer.Deserialize<GetItemsResponse>(responseText, options);
        string url = apiResponse?.ItemsResult?.Items?.FirstOrDefault()?.DetailPageUrl ?? "";

        return string.IsNullOrWhiteSpace(url)
            ? (false, "", "Creators API でURLを取得できませんでした。", "")
            : (true, url, "", ExtractFirstItemJson(responseText));
    }

    // 応答から商品1件分のJSONをそのまま取り出す。取り出せなくてもリンク作成は成功させる。
    private static string ExtractFirstItemJson(string responseText)
    {
        try
        {
            using var document = JsonDocument.Parse(responseText);
            if (document.RootElement.TryGetProperty("itemsResult", out JsonElement itemsResult)
                && itemsResult.TryGetProperty("items", out JsonElement items)
                && items.ValueKind == JsonValueKind.Array
                && items.GetArrayLength() > 0)
            {
                return items[0].GetRawText();
            }
        }
        catch (JsonException)
        {
        }

        return "";
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
