using System.Net.Http;

namespace YukkuriMovieMaker.Plugin.Community.Voice.AivisCloudAPI.API
{
    /// <summary>
    /// Aivisの音声合成レスポンス（ヘッダー）を表すクラス
    /// </summary>
    public sealed class SynthesizeHeaderResponse
    {
        /// <summary>
        /// Content-Disposition: 生成された音声ファイルの推奨ファイル名 (例: 20250720-054144_a59cb814-0083-4369-8542-f51a29e72af7.mp3)
        /// </summary>
        public string? ContentDisposition { get; init; }

        /// <summary>
        /// X-Aivis-Billing-Mode: 課金モード（PayAsYouGo, Subscription）
        /// </summary>
        public BillingMode BillingMode { get; init; }

        /// <summary>
        /// X-Aivis-Character-Count: 今回の音声合成で消費されたテキストの文字数
        /// </summary>
        public int CharacterCount { get; init; }


        /// <summary>
        /// 従量課金モード時の追加ヘッダー
        /// </summary>
        public PayAsYouGoDetails? PayAsYouGo { get; init; }
        /// <summary>
        /// 定額プランモード時の追加ヘッダー
        /// </summary>
        public SubscriptionDetails? Subscription { get; init; }

        /// <summary>
        /// 従量課金モード時の追加ヘッダー
        /// </summary>
        /// <param name="CreditsUsed">X-Aivis-Credits-Used: 今回の音声合成で消費されたクレジット量</param>
        /// <param name="CreditsRemaining">X-Aivis-Credits-Remaining: 音声合成後のクレジット残高</param>
        public record PayAsYouGoDetails(
            int CreditsUsed,
            int CreditsRemaining)
        {
            public static PayAsYouGoDetails FromHttpResponse(HttpResponseMessage response)
            {
                ArgumentNullException.ThrowIfNull(response);
                var creditsUsed = int.TryParse(GetHeaderValue(response, "X-Aivis-Credits-Used"), out var used) ? used : 0;
                var creditsRemaining = int.TryParse(GetHeaderValue(response, "X-Aivis-Credits-Remaining"), out var remaining) ? remaining : 0;
                return new PayAsYouGoDetails(creditsUsed, creditsRemaining);
            }
        }

        /// <summary>
        /// 定額プランモード時の追加ヘッダー
        /// </summary>
        /// <param name="RequestsLimit">X-Aivis-RateLimit-Requests-Limit: レート制限の単位時間内に許可されるリクエストの最大数 (現在は 10 リクエスト固定)</param>
        /// <param name="RequestsRemaining">X-Aivis-RateLimit-Requests-Remaining: レート制限の単位時間内に許可されるリクエストの残数</param>
        /// <param name="RequestsReset">X-Aivis-RateLimit-Requests-Reset: レート制限が完全にリセットされるまでの残り秒数 (整数、端数切り上げ)</param>
        public record SubscriptionDetails(
            int RequestsLimit,
            int RequestsRemaining,
            TimeSpan RequestsReset)
        {
            public static SubscriptionDetails FromHttpResponse(HttpResponseMessage response)
            {
                ArgumentNullException.ThrowIfNull(response);
                var requestsLimit = int.TryParse(GetHeaderValue(response, "X-Aivis-RateLimit-Requests-Limit"), out var limit) ? limit : 0;
                var requestsRemaining = int.TryParse(GetHeaderValue(response, "X-Aivis-RateLimit-Requests-Remaining"), out var remaining) ? remaining : 0;
                var requestsResetSeconds = int.TryParse(GetHeaderValue(response, "X-Aivis-RateLimit-Requests-Reset"), out var reset) ? reset : 0;
                return new SubscriptionDetails(requestsLimit, requestsRemaining, TimeSpan.FromSeconds(requestsResetSeconds));
            }
        }

        public static SynthesizeHeaderResponse FromHttpResponse(HttpResponseMessage response)
        {
            ArgumentNullException.ThrowIfNull(response);

            //共通ヘッダー
            var contentDisposition = GetHeaderValue(response, "Content-Disposition");
            var billingMode = ParseBillingMode(GetHeaderValue(response, "X-Aivis-Billing-Mode"));
            var characterCount = int.TryParse(GetHeaderValue(response, "X-Aivis-Character-Count"), out var count) ? count : 0;

            //従量課金モード時の追加ヘッダー
            PayAsYouGoDetails? payAsYouGo = null;
            if (billingMode is BillingMode.PayAsYouGo)
                payAsYouGo = PayAsYouGoDetails.FromHttpResponse(response);

            //定額プランモード時の追加ヘッダー
            SubscriptionDetails? subscription = null;
            if (billingMode is BillingMode.Subscription)
                subscription = SubscriptionDetails.FromHttpResponse(response);

            return new SynthesizeHeaderResponse
            {
                ContentDisposition = contentDisposition,
                BillingMode = billingMode,
                CharacterCount = characterCount,
                PayAsYouGo = payAsYouGo,
                Subscription = subscription
            };
        }

        static BillingMode ParseBillingMode(string billingMode)
        {
            return billingMode switch
            {
                "PayAsYouGo" => BillingMode.PayAsYouGo,
                "Subscription" => BillingMode.Subscription,
                _ => BillingMode.Unknown
            };
        }
        static string GetHeaderValue(HttpResponseMessage response, string headerName)
        {
            if (response.Headers.TryGetValues(headerName, out var values))
            {
                return values.FirstOrDefault() ?? string.Empty;
            }
            return string.Empty;
        }
    }
}
