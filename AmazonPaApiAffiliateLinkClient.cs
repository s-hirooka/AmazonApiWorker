using System;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace AmazonPaApiLinkSample
{
    /// <summary>
    /// PA-API 5.0 で ASIN から DetailPageURL を取得するクラス
    /// 返る URL は通常のアフィリエイトURL（短縮URLではない）
    /// </summary>
    public sealed class AmazonPaApiAffiliateLinkClient : IDisposable
    {
        private readonly HttpClient _httpClient;

        public string AccessKey { get; }
        public string SecretKey { get; }
        public string PartnerTag { get; }
        public string Host { get; }
        public string Region { get; }
        public string Marketplace { get; }

        /// <summary>
        /// 日本向け既定値:
        /// Host = webservices.amazon.co.jp
        /// Region = us-west-2
        /// Marketplace = www.amazon.co.jp
        /// </summary>
        public AmazonPaApiAffiliateLinkClient(
            string accessKey,
            string secretKey,
            string partnerTag,
            string host = "webservices.amazon.co.jp",
            string region = "us-west-2",
            string marketplace = "www.amazon.co.jp",
            HttpMessageHandler? handler = null)
        {
            AccessKey = accessKey ?? throw new ArgumentNullException(nameof(accessKey));
            SecretKey = secretKey ?? throw new ArgumentNullException(nameof(secretKey));
            PartnerTag = partnerTag ?? throw new ArgumentNullException(nameof(partnerTag));
            Host = host ?? throw new ArgumentNullException(nameof(host));
            Region = region ?? throw new ArgumentNullException(nameof(region));
            Marketplace = marketplace ?? throw new ArgumentNullException(nameof(marketplace));

            _httpClient = handler == null ? new HttpClient() : new HttpClient(handler, disposeHandler: true);
            _httpClient.Timeout = TimeSpan.FromSeconds(30);
        }

        /// <summary>
        /// ASIN から通常アフィリエイトURL（DetailPageURL）を取得
        /// </summary>
        public async Task<AffiliateLinkResult> GetAffiliateLinkByAsinAsync(
            string asin,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(asin))
                throw new ArgumentException("ASIN is required.", nameof(asin));

            const string canonicalUri = "/paapi5/getitems";
            const string service = "ProductAdvertisingAPI";
            const string target = "com.amazon.paapi5.v1.ProductAdvertisingAPIv1.GetItems";

            var requestBodyObject = new
            {
                ItemIds = new[] { asin },
                PartnerTag = PartnerTag,
                PartnerType = "Associates",
                Marketplace = Marketplace,
                Resources = new[]
                {
                    "ItemInfo.Title"
                }
            };

            string requestJson = JsonSerializer.Serialize(requestBodyObject);
            string amzDate = DateTime.UtcNow.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);
            string dateStamp = DateTime.UtcNow.ToString("yyyyMMdd", CultureInfo.InvariantCulture);

            var headers = new System.Collections.Generic.SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["content-encoding"] = "amz-1.0",
                ["content-type"] = "application/json; charset=utf-8",
                ["host"] = Host,
                ["x-amz-date"] = amzDate,
                ["x-amz-target"] = target
            };

            string canonicalHeaders = string.Join("", headers.Select(h => $"{h.Key}:{h.Value.Trim()}\n"));
            string signedHeaders = string.Join(";", headers.Keys);
            string payloadHash = ToHexString(SHA256Hash(requestJson));

            string canonicalRequest =
                "POST\n" +
                canonicalUri + "\n" +
                "\n" +
                canonicalHeaders + "\n" +
                signedHeaders + "\n" +
                payloadHash;

            string algorithm = "AWS4-HMAC-SHA256";
            string credentialScope = $"{dateStamp}/{Region}/{service}/aws4_request";
            string stringToSign =
                algorithm + "\n" +
                amzDate + "\n" +
                credentialScope + "\n" +
                ToHexString(SHA256Hash(canonicalRequest));

            byte[] signingKey = GetSignatureKey(SecretKey, dateStamp, Region, service);
            string signature = ToHexString(HmacSHA256(signingKey, stringToSign));

            string authorizationHeader =
                $"{algorithm} " +
                $"Credential={AccessKey}/{credentialScope}, " +
                $"SignedHeaders={signedHeaders}, " +
                $"Signature={signature}";

            using var request = new HttpRequestMessage(HttpMethod.Post, $"https://{Host}{canonicalUri}");
            request.Content = new StringContent(requestJson, Encoding.UTF8, "application/json");
            request.Headers.TryAddWithoutValidation("content-encoding", "amz-1.0");
            request.Headers.TryAddWithoutValidation("x-amz-date", amzDate);
            request.Headers.TryAddWithoutValidation("x-amz-target", target);
            request.Headers.TryAddWithoutValidation("Authorization", authorizationHeader);
            request.Headers.Host = Host;

            using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            string responseText = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                return new AffiliateLinkResult
                {
                    Success = false,
                    Asin = asin,
                    ErrorMessage = $"HTTP {(int)response.StatusCode} {response.StatusCode}: {responseText}",
                    RawResponse = responseText
                };
            }

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            var apiResponse = JsonSerializer.Deserialize<GetItemsResponse>(responseText, options);

            if (apiResponse?.ItemsResult?.Items == null || apiResponse.ItemsResult.Items.Length == 0)
            {
                return new AffiliateLinkResult
                {
                    Success = false,
                    Asin = asin,
                    ErrorMessage = "No item returned.",
                    RawResponse = responseText
                };
            }

            var item = apiResponse.ItemsResult.Items[0];

            return new AffiliateLinkResult
            {
                Success = true,
                Asin = item.ASIN ?? asin,
                Title = item.ItemInfo?.Title?.DisplayValue ?? string.Empty,
                AffiliateUrl = item.DetailPageURL ?? string.Empty,
                ShortUrl = string.Empty, // PA-API公式で短縮URL取得は見当たらない
                RawResponse = responseText
            };
        }

        private static byte[] SHA256Hash(string data)
        {
            using var sha256 = SHA256.Create();
            return sha256.ComputeHash(Encoding.UTF8.GetBytes(data));
        }

        private static byte[] HmacSHA256(byte[] key, string data)
        {
            using var hmac = new HMACSHA256(key);
            return hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
        }

        private static byte[] HmacSHA256(string key, string data)
        {
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(key));
            return hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
        }

        private static byte[] GetSignatureKey(string secretKey, string dateStamp, string regionName, string serviceName)
        {
            byte[] kDate = HmacSHA256("AWS4" + secretKey, dateStamp);
            byte[] kRegion = HmacSHA256(kDate, regionName);
            byte[] kService = HmacSHA256(kRegion, serviceName);
            byte[] kSigning = HmacSHA256(kService, "aws4_request");
            return kSigning;
        }

        private static string ToHexString(byte[] bytes)
        {
            var sb = new StringBuilder(bytes.Length * 2);
            foreach (byte b in bytes)
            {
                sb.Append(b.ToString("x2", CultureInfo.InvariantCulture));
            }
            return sb.ToString();
        }

        public void Dispose()
        {
            _httpClient.Dispose();
        }
    }

    public sealed class AffiliateLinkResult
    {
        public bool Success { get; set; }
        public string Asin { get; set; } = "";
        public string Title { get; set; } = "";
        public string AffiliateUrl { get; set; } = "";
        public string ShortUrl { get; set; } = "";
        public string ErrorMessage { get; set; } = "";
        public string RawResponse { get; set; } = "";
    }

    public sealed class GetItemsResponse
    {
        [JsonPropertyName("ItemsResult")]
        public ItemsResult? ItemsResult { get; set; }
    }

    public sealed class ItemsResult
    {
        [JsonPropertyName("Items")]
        public Item[]? Items { get; set; }
    }

    public sealed class Item
    {
        [JsonPropertyName("ASIN")]
        public string? ASIN { get; set; }

        [JsonPropertyName("DetailPageURL")]
        public string? DetailPageURL { get; set; }

        [JsonPropertyName("ItemInfo")]
        public ItemInfo? ItemInfo { get; set; }
    }

    public sealed class ItemInfo
    {
        [JsonPropertyName("Title")]
        public TitleInfo? Title { get; set; }
    }

    public sealed class TitleInfo
    {
        [JsonPropertyName("DisplayValue")]
        public string? DisplayValue { get; set; }
    }
}