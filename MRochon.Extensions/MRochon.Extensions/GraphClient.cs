using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace MRochon.Extensions
{
    public class GraphOptions
    {
        public string? LocalIdentityIssuer { get; set; }
        public string? ExtensionAppId { get; set; }
    }
    public class GraphClient
    {
        private readonly ILogger<GraphClient> _logger;
        private readonly IConfidentialClientApplication _msal;
        private readonly HttpClient _http;
        private readonly IOptions<GraphOptions> _options;
        public GraphClient(ILogger<GraphClient> logger,
            IConfidentialClientApplication msal,
            HttpClient http,
            IOptions<GraphOptions> options)
        {
            _logger = logger;
            _msal = msal;
            _http = http;
            _options = options;
        }

        public async Task<JsonNode?> FindUserAsync(string idValue, string? idIssuer = null)
        {
            if(String.IsNullOrEmpty(idIssuer))
            {
                idIssuer = _options.Value.LocalIdentityIssuer;
            }
            var tokens = await _msal.AcquireTokenForClient(new string[] { ".default" }).ExecuteAsync();
            _http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tokens.AccessToken);
            var req = new HttpRequestMessage(HttpMethod.Get, $"https://graph.microsoft.com/v1.0/users?$filter=(identities/any(i:i/issuer eq '{idIssuer}' and i/issuerAssignedId eq '{idValue}'))");
            var resp = await _http.SendAsync(req);
            var respBody = await resp.Content.ReadAsStringAsync();
            if (resp.IsSuccessStatusCode)
            {
                return ((JsonArray)(JsonNode.Parse(respBody)["value"])).FirstOrDefault();
            }
            _logger.LogError(respBody);
            return null;
        }

        public async Task<string?> NewUserAsync(string json)
        {
            if (_options.Value != null)
            {
                if (!String.IsNullOrEmpty(_options.Value.LocalIdentityIssuer))
                    json = json.Replace("{issuer}", _options.Value.LocalIdentityIssuer);
                if (!String.IsNullOrEmpty(_options.Value.ExtensionAppId))
                {
                    var extId = _options.Value.ExtensionAppId.Replace("-", "");
                    json = json.Replace("extension_", $"extension_{extId}");
                }
            }
            var tokens = await _msal.AcquireTokenForClient(new string[] { ".default" }).ExecuteAsync();
            _http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tokens.AccessToken);
            var req = new HttpRequestMessage(HttpMethod.Post, "https://graph.microsoft.com/v1.0/users")
            {
                Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
            };
            var resp = await _http.SendAsync(req);
            var respBody = await resp.Content.ReadAsStringAsync();
            if (resp.IsSuccessStatusCode)
            {
                var userId = JsonNode.Parse(respBody)["id"]!.GetValue<string>();
                return userId;
            }
            _logger.LogError(respBody);
            return null;
        }

        public async Task<bool> AddToGroupAsync(string groupdId, string membId, bool asOwner = false)
        {
            var tokens = await _msal.AcquireTokenForClient(new string[] { ".default" }).ExecuteAsync();
            _http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tokens.AccessToken);
            var membType = asOwner ? "owners" : "members";
            var objType = asOwner ? "users" : "directoryObjects";
            var resp = await _http.PostAsync($"https://graph.microsoft.com/V1.0/groups/{groupdId}/{membType}/$ref",
                new StringContent($"{{\"@odata.id\":\"https://graph.microsoft.com/v1.0/{objType}/{membId}\"}}", Encoding.UTF8, "application/json"));
            if (resp.IsSuccessStatusCode)
                return true;
            _logger.LogError(await resp.Content.ReadAsStringAsync());
            return false;
        }
    }
}
