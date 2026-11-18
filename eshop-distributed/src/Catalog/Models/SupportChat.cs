namespace Catalog.Models;

// One earlier message in the conversation. Role is "user" or "assistant".
public record ChatTurn(string Role, string Content);

// What the client sends to the support chat. History is optional and lets the
// assistant understand follow-up questions like "is it waterproof?".
public record SupportRequest(string Question, List<ChatTurn>? History = null);

// What the assistant answers. DurationMs is handy in the demo to show how long the LLM took.
public record SupportResponse(string Answer, long DurationMs);
