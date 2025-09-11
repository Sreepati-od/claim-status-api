using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Azure.AI.OpenAI;
using Azure;

namespace ClaimStatusApi.Controllers
{
	[ApiController]
	[Route("claims")]
	public class ClaimController : ControllerBase
	{
		private readonly string claimsPath = "../../mocks/claims.json";
		private readonly string notesPath = "../../mocks/notes.json";
		private readonly string openAiEndpoint;
		private readonly string openAiKey;
		private readonly string openAiDeployment;

		public ClaimController()
		{
			openAiEndpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT") ?? "";
			openAiKey = Environment.GetEnvironmentVariable("AZURE_OPENAI_KEY") ?? "";
			openAiDeployment = Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT") ?? "gpt-4o-mini";
		}

		[HttpGet("{id}")]
		public async Task<IActionResult> GetClaim(string id)
		{
			if (!System.IO.File.Exists(claimsPath))
				return NotFound();
			var claims = JsonSerializer.Deserialize<List<JsonObject>>(await System.IO.File.ReadAllTextAsync(claimsPath));
			var claim = claims?.Find(c => c["id"]?.ToString() == id);
			if (claim == null) return NotFound();
			return Ok(claim);
		}

		[HttpPost("{id}/summarize")]
		public async Task<IActionResult> SummarizeClaim(string id)
		{
			if (!System.IO.File.Exists(notesPath))
				return NotFound();
			var notesJson = await System.IO.File.ReadAllTextAsync(notesPath);
			var notesDoc = JsonNode.Parse(notesJson)?.AsObject();
			if (notesDoc == null || !notesDoc.ContainsKey(id))
				return NotFound();
			var notes = notesDoc[id]?.AsArray()?.Select(n => n.ToString()).ToList() ?? new List<string>();
			if (notes.Count == 0)
				return NotFound();

			// Compose prompt for OpenAI
			var prompt = $"Summarize the following claim notes in a concise way. Provide: summary, customerSummary, adjusterSummary, and nextStep. Notes: {string.Join(" ", notes)}";

			string summary = "";
			string customerSummary = "";
			string adjusterSummary = "";
			string nextStep = "";

			if (!string.IsNullOrEmpty(openAiEndpoint) && !string.IsNullOrEmpty(openAiKey))
			{
				try
				{
					var client = new OpenAIClient(new Uri(openAiEndpoint), new AzureKeyCredential(openAiKey));
					var chatCompletionsOptions = new ChatCompletionsOptions()
					{
						Messages =
						{
							new ChatMessage(ChatRole.System, "You are a helpful insurance claim summarizer."),
							new ChatMessage(ChatRole.User, prompt)
						},
						MaxTokens = 256,
						Temperature = 0.2f
					};
					var response = await client.GetChatCompletionsAsync(openAiDeployment, chatCompletionsOptions);
					var content = response.Value.Choices[0].Message.Content;
					// Try to parse as JSON, fallback to plain text
					try
					{
						var json = JsonNode.Parse(content)?.AsObject();
						summary = json?["summary"]?.ToString() ?? content;
						customerSummary = json?["customerSummary"]?.ToString() ?? "";
						adjusterSummary = json?["adjusterSummary"]?.ToString() ?? "";
						nextStep = json?["nextStep"]?.ToString() ?? "";
					}
					catch
					{
						summary = content;
					}
				}
				catch (Exception ex)
				{
					summary = $"OpenAI error: {ex.Message}";
				}
			}
			else
			{
				summary = string.Join(" ", notes);
				customerSummary = notes.FirstOrDefault() ?? "";
				adjusterSummary = notes.Skip(1).FirstOrDefault() ?? "";
				nextStep = notes.LastOrDefault() ?? "";
			}

			var result = new {
				summary,
				customerSummary,
				adjusterSummary,
				nextStep
			};
			return Ok(result);
		}
	}
}
