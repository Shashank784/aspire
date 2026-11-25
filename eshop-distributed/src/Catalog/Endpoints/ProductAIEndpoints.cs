namespace Catalog.Endpoints;

// AI endpoints. Open to everyone (no login), like the product list.
// Through the BFF they are reachable at /api/products/ai/...
public static class ProductAIEndpoints
{
    private const int MaxQuestionLength = 500;

    public static void MapProductAIEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/products/ai");

        // Simple Q&A: one question, one answer. Easy to try in a browser.
        // GET /products/ai/support?question=What can I use to cook outdoors?
        group.MapGet("/support", (string question, ProductAIService aiService, CancellationToken cancellationToken) =>
            AskAsync(new SupportRequest(question), aiService, cancellationToken))
        .WithName("AskProductSupport")
        .Produces<SupportResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status503ServiceUnavailable);

        // Chat: question plus earlier messages, so follow-up questions work.
        // POST /products/ai/chat  { "question": "...", "history": [ { "role": "user", "content": "..." } ] }
        group.MapPost("/chat", (SupportRequest request, ProductAIService aiService, CancellationToken cancellationToken) =>
            AskAsync(request, aiService, cancellationToken))
        .WithName("ChatProductSupport")
        .Produces<SupportResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status503ServiceUnavailable);

        // Semantic search: products ranked by meaning, with a similarity score.
        // GET /products/ai/search?query=something to keep me dry&top=3
        group.MapGet("/search", async (string query, ProductAIService aiService, CancellationToken cancellationToken, int top = 5) =>
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return Results.BadRequest("Please type what you are looking for.");
            }

            if (query.Length > MaxQuestionLength)
            {
                return Results.BadRequest($"The search text must be at most {MaxQuestionLength} characters.");
            }

            top = Math.Clamp(top, 1, 20);

            try
            {
                return Results.Ok(await aiService.SearchProductsAsync(query.Trim(), top, cancellationToken));
            }
            catch (Exception ex) when (IsModelUnavailable(ex, cancellationToken))
            {
                return ModelUnavailable();
            }
        })
        .WithName("SemanticSearchProducts")
        .Produces<SemanticSearchResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status503ServiceUnavailable);
    }

    // Ollama is not reachable yet, the model is still downloading, or it took too long.
    private static bool IsModelUnavailable(Exception ex, CancellationToken cancellationToken) =>
        ex is HttpRequestException or TimeoutException or OllamaSharp.Models.Exceptions.OllamaException
        || (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested);

    private static IResult ModelUnavailable() =>
        Results.Problem(
            title: "The AI assistant is not available right now",
            detail: "The AI model may still be starting or downloading. Please try again in a minute.",
            statusCode: StatusCodes.Status503ServiceUnavailable);

    private static async Task<IResult> AskAsync(SupportRequest request, ProductAIService aiService, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Question))
        {
            return Results.BadRequest("Please ask a question.");
        }

        if (request.Question.Length > MaxQuestionLength)
        {
            return Results.BadRequest($"The question must be at most {MaxQuestionLength} characters.");
        }

        try
        {
            return Results.Ok(await aiService.AskAsync(request, cancellationToken));
        }
        catch (Exception ex) when (IsModelUnavailable(ex, cancellationToken))
        {
            return ModelUnavailable();
        }
    }
}
