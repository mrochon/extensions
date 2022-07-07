#r "Newtonsoft.Json"

using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;
using Newtonsoft.Json;

public static async Task<IActionResult> Run(HttpRequest req, ILogger log)
{
    log.LogInformation("AddUserToGroups starting...");
    string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
    dynamic data = JsonConvert.DeserializeObject(requestBody);
    string objectId = data?.objectId;
    string groups = data?.groups;
    log.LogInformation($"AddUserToGroups for userObjectId={objectId}&groups={groups}");
    if (String.IsNullOrEmpty(objectId) || String.IsNullOrEmpty(groups))
    {
        log.LogError("Null or empty arguments");
        return new BadRequestObjectResult(new { version = "1.0.0", status = 409, userMessage = "Bad arguments" });        
    }

    var tenant_id = Environment.GetEnvironmentVariable("B2C:tenant_id");
    var client_id = Environment.GetEnvironmentVariable("B2C:client_id");
    var client_secret = Environment.GetEnvironmentVariable("B2C:client_secret");

    using (var http = new HttpClient())
    {
        try
        {
            var resp = await http.PostAsync($"https://login.microsoftonline.com/{tenant_id}/oauth2/v2.0/token",
                new FormUrlEncodedContent(new List<KeyValuePair<String, String>>
                {
                    new KeyValuePair<string,string>("client_id", client_id),
                    new KeyValuePair<string,string>("scope", "https://graph.microsoft.com/.default"),
                    new KeyValuePair<string,string>("client_secret", client_secret),
                    new KeyValuePair<string,string>("grant_type", "client_credentials")
                }));
            if (!resp.IsSuccessStatusCode)
                return new BadRequestObjectResult(new { version = "1.0.0", status = 409, userMessage = "Authorization failure" });
            dynamic tokens = JsonConvert.DeserializeObject(await resp.Content.ReadAsStringAsync());
            http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", ((string)(tokens.access_token)));   
            foreach(var groupId in groups.Split(','))
            {
                var json = await http.PostAsync(
                                $"https://graph.microsoft.com/v1.0/groups/{groupId}/members/$ref",
                                new StringContent(
                                    $"{{\"@odata.id\": \"https://graph.microsoft.com/v1.0/directoryObjects/{objectId}\"}}",
                                    System.Text.Encoding.UTF8,
                                "application/json"));                
                    log.LogInformation($"AddUserToGroup added user {objectId} to group {groupId}");
            }        
            return new OkResult();
        }
        catch (Exception ex)
        {
            log.LogError($"AddUserToGroups: {ex.Message}");
            return new BadRequestObjectResult(new { version = "1.0.0", status = 409, userMessage = ex.Message });
        }
    }
}