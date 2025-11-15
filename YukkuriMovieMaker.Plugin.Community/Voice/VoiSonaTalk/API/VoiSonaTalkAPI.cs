using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Voice.VoiSonaTalk.API
{
    internal partial class VoiSonaTalkAPI(string mail, string password, int port)
    {
        readonly string mail = mail;
        readonly string password = password;

        string BaseUrl => $"http://localhost:{port}";
        string VoiceBaseUrl => BaseUrl + "/api/talk/v1/voices";
        string TextAnalysisBaseUrl => BaseUrl + "/api/talk/v1/text-analyses";
        string SpeechSynthesisBaseUrl => BaseUrl + "/api/talk/v1/speech-syntheses";

        void ApplyAuthentication(HttpRequestMessage request)
        {
            var basicText = $"{mail}:{password}";
            var basicArray = Encoding.ASCII.GetBytes(basicText);
            var basicBase64 = Convert.ToBase64String(basicArray);
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", basicBase64);
        }

        #region Voices
        public async Task<VoiceBaseInformation[]> GetVoiceAsync()
        {
            var url = VoiceBaseUrl;
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            ApplyAuthentication(request);

            var client = HttpClientFactory.Client;
            using var response = await client.SendAsync(request);
            await ThrowIfErrorResponse(response);

            var text = await response.Content.ReadAsStringAsync();
            var voicesResponse = Json.Json.LoadFromText<VoiceBaseInformations>(text) ?? throw new NullReferenceException("VoicesResponse is null");

            return voicesResponse.Items;
        }

        public async Task<VoiceInformation> GetVoiceAsync(string name, string version)
        {
            var url = VoiceBaseUrl + $"/{name}/{version}";
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            ApplyAuthentication(request);

            var client = HttpClientFactory.Client;
            using var response = await client.SendAsync(request);
            await ThrowIfErrorResponse(response);

            var text = await response.Content.ReadAsStringAsync();
            var info = Json.Json.LoadFromText<VoiceInformation>(text) ?? throw new NullReferenceException("VoiceInformation is null");
            return info;
        }
        #endregion

        #region SpeechSynthesis
        public async Task<RequestSpeechSynthesisResponse> RequestSpeechSynthesisAsync(SpeechSynthesisRequest speechSynthesisRequest)
        {
            var url = SpeechSynthesisBaseUrl;
            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            ApplyAuthentication(request);

            var speechSynthesisRequestJson = Json.Json.GetJsonText(speechSynthesisRequest) ?? throw new NullReferenceException("SpeechSynthesisRequest is null");
            using var content = new StringContent(speechSynthesisRequestJson);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            request.Content = content;

            var client = HttpClientFactory.Client;
            using var response = await client.SendAsync(request);
            await ThrowIfErrorResponse(response);

            var responseText = await response.Content.ReadAsStringAsync();
            var requestSpeechSynthesisResponse = Json.Json.LoadFromText<RequestSpeechSynthesisResponse>(responseText) ?? throw new NullReferenceException("RequestSpeechSynthesisResponse is null");
            return requestSpeechSynthesisResponse;
        }

        public async Task<SpeechSynthesisBaseInformation[]> GetSpeechSynthesisAsync()
        {
            var url = SpeechSynthesisBaseUrl;
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            ApplyAuthentication(request);

            var client = HttpClientFactory.Client;
            using var response = await client.SendAsync(request);
            await ThrowIfErrorResponse(response);

            var text = await response.Content.ReadAsStringAsync();
            var speechSynthesisResponse = Json.Json.LoadFromText<SpeechSynthesisBaseInformations>(text) ?? throw new NullReferenceException("SpeechSynthesisBaseInformations is null");
            return speechSynthesisResponse.Items;
        }

        public async Task<SpeechSynthesisInformation> GetSpeechSynthesisAsync(string uuid)
        {
            var url = SpeechSynthesisBaseUrl + $"/{uuid}";
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            ApplyAuthentication(request);

            var client = HttpClientFactory.Client;
            using var response = await client.SendAsync(request);
            await ThrowIfErrorResponse(response);

            var text = await response.Content.ReadAsStringAsync();
            var speechSynthesisInformation = Json.Json.LoadFromText<SpeechSynthesisInformation>(text) ?? throw new NullReferenceException("SpeechSynthesisInformation is null");
            return speechSynthesisInformation;
        }

        public async Task DeleteSpeechSynthesisAsync(string uuid)
        {
            var url = SpeechSynthesisBaseUrl + $"/{uuid}";
            using var request = new HttpRequestMessage(HttpMethod.Delete, url);
            ApplyAuthentication(request);

            var client = HttpClientFactory.Client;
            using var response = await client.SendAsync(request);
            await ThrowIfErrorResponse(response);
        }
        #endregion

        #region TextAnalysis
        public async Task<RequestTextAnalysisResponse> RequestTextAnalysisAsync(string text, string language)
        {
            var url = TextAnalysisBaseUrl;
            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            ApplyAuthentication(request);

            var textAnalysisRequest = new TextAnalysisRequest(true, language, text);
            var textAnalysisRequestJson = Json.Json.GetJsonText(textAnalysisRequest) ?? throw new NullReferenceException("TextAnalysisRequest is null");
            using var content = new StringContent(textAnalysisRequestJson);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            request.Content = content;

            var client = HttpClientFactory.Client;
            using var response = await client.SendAsync(request);
            await ThrowIfErrorResponse(response);

            var responseText = await response.Content.ReadAsStringAsync();
            var requestTextAnalysisResponse = Json.Json.LoadFromText<RequestTextAnalysisResponse>(responseText) ?? throw new NullReferenceException("RequestTextAnalysisResponse is null");
            return requestTextAnalysisResponse;
        }

        public async Task<TextAnalysisBaseInformation[]> GetTextAnalysisAsync()
        {
            var url = TextAnalysisBaseUrl;
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            ApplyAuthentication(request);

            var client = HttpClientFactory.Client;
            using var response = await client.SendAsync(request);
            await ThrowIfErrorResponse(response);

            var text = await response.Content.ReadAsStringAsync();
            var textAnalysisResponse = Json.Json.LoadFromText<TextAnalysisBaseInformations>(text) ?? throw new NullReferenceException("TextAnalysisBaseInformations is null");
            return textAnalysisResponse.Items;
        }

        public async Task<TextAnalysisInformation> GetTextAnalysisAsync(string uuid)
        {
            var url = TextAnalysisBaseUrl + $"/{uuid}";
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            ApplyAuthentication(request);

            var client = HttpClientFactory.Client;
            using var response = await client.SendAsync(request);
            await ThrowIfErrorResponse(response);

            var text = await response.Content.ReadAsStringAsync();
            var textAnalysisInformation = Json.Json.LoadFromText<TextAnalysisInformation>(text) ?? throw new NullReferenceException("TextAnalysisInformation is null");
            return textAnalysisInformation;
        }

        public async Task DeleteTextAnalysisAsync(string uuid)
        {
            var url = TextAnalysisBaseUrl + $"/{uuid}";
            using var request = new HttpRequestMessage(HttpMethod.Delete, url);
            ApplyAuthentication(request);
            var client = HttpClientFactory.Client;
            using var response = await client.SendAsync(request);
            await ThrowIfErrorResponse(response);
        }
        #endregion

        static async Task ThrowIfErrorResponse(HttpResponseMessage response)
        {
            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode is System.Net.HttpStatusCode.Unauthorized)
                    throw new VoiSonaTalkException(Texts.UnauthorizedMessage);

                try
                {
                    var errorText = await response.Content.ReadAsStringAsync();
                    var error = Json.Json.LoadFromText<ErrorMessage>(errorText);
                    if (error is not null)
                        throw new VoiSonaTalkException($"{error.Title}\r\n{error.Detail}");
                }
                catch(VoiSonaTalkException)
                {
                    throw;
                }
                catch
                {
                    response.EnsureSuccessStatusCode();
                    return;
                }
                response.EnsureSuccessStatusCode();
            }
        }
    }

    internal class VoiSonaTalkException(string message) : Exception(message)
    {
    }
}
