using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;

[ApiController]
[Route("claims")]
public class ClaimController : ControllerBase
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IWebHostEnvironment _env;
    public ClaimController(IHttpClientFactory httpClientFactory, IWebHostEnvironment env)
    {
        _httpClientFactory = httpClientFactory;
        _env = env;
    }

    [HttpGet("{id}")]
    public IActionResult GetClaim(string id)
    {
        var claimsPath = Path.Combine(_env.ContentRootPath, "mocks", "claims.json");
        if (!System.IO.File.Exists(claimsPath))
            return NotFound();
        var claims = JsonSerializer.Deserialize<List<ClaimModel>>(System.IO.File.ReadAllText(claimsPath));
        var claim = claims?.FirstOrDefault(c => c.id == id);
        if (claim == null) return NotFound();
        return Ok(claim);
    }

    [HttpPost("{id}/summarize")]
    public async Task<IActionResult> SummarizeClaim(string id)
    {
        var notesPath = Path.Combine(_env.ContentRootPath, "mocks", "notes.json");
        if (!System.IO.File.Exists(notesPath))
            return NotFound();
        var notesDict = JsonSerializer.Deserialize<Dictionary<string, List<string>>>(System.IO.File.ReadAllText(notesPath));
        if (!notesDict.ContainsKey(id)) return NotFound();
        var notes = string.Join("\n", notesDict[id]);
        var prompt = $@"System: You are a professional insurance claims summarizer.\nUser: Given the following claim notes, produce:\n- a short 1-sentence customer-facing summary,\n- a 3–5 sentence technical adjuster summary,\n- a brief list of recommended next steps (3 bullets).\nRespond in JSON with keys: summary, customerSummary, adjusterSummary, nextStep.\n\nNotes:\n{notes}";
        var openAiEndpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT");
        var openAiKey = Environment.GetEnvironmentVariable("AZURE_OPENAI_KEY");
        if (string.IsNullOrEmpty(openAiEndpoint) || string.IsNullOrEmpty(openAiKey))
            return StatusCode(500, new { error = "OpenAI endpoint/key not configured" });
        var client = _httpClientFactory.CreateClient();
        client.BaseAddress = new Uri(openAiEndpoint);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", openAiKey);
        var requestBody = new
        {
            messages = new[] {
                new { role = "system", content = "You are a professional insurance claims summarizer." },
                new { role = "user", content = prompt }
            }
        };
        var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
        try
        {
            var response = await client.PostAsync("/openai/deployments/<deployment>/chat/completions?api-version=2023-05-15", content);
            if (!response.IsSuccessStatusCode)
                return StatusCode((int)response.StatusCode, new { error = "OpenAI call failed" });
            var respJson = await response.Content.ReadAsStringAsync();
            // Extract the model's response (assume response.choices[0].message.content is the JSON)
            using var doc = JsonDocument.Parse(respJson);
            var contentJson = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
            var summaryObj = JsonSerializer.Deserialize<object>(contentJson!);
            return Ok(summaryObj);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    public class ClaimModel
    {
        public string id { get; set; }
        public string status { get; set; }
        public string policyHolder { get; set; }
        public string lossDate { get; set; }
        public double amount { get; set; }
        public string adjuster { get; set; }
    }
}
