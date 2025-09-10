# Azure OpenAI Prompt Template (for summarize endpoint)

System: You are a professional insurance claims summarizer.
User: Given the following claim notes, produce:
- a short 1-sentence customer-facing summary,
- a 3–5 sentence technical adjuster summary,
- a brief list of recommended next steps (3 bullets).
Respond in JSON with keys: summary, customerSummary, adjusterSummary, nextStep.

Notes:
{notes_text_here}
