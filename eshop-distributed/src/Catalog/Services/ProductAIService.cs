using System.Diagnostics;
using System.Globalization;
using System.Text;
using Microsoft.Extensions.AI;

namespace Catalog.Services;

// Customer support assistant for the shop.
//
// This is simple RAG (Retrieval-Augmented Generation):
//   1. Retrieve: load our real products (from Redis/Postgres via ProductService).
//   2. Augment:  put them into the system prompt.
//   3. Generate: the LLM answers using ONLY those products, so it does not make things up.
//
// The code only depends on IChatClient (Microsoft.Extensions.AI), not on Ollama directly.
public class ProductAIService(IChatClient chatClient, ProductService productService, ILogger<ProductAIService> logger)
{
    // Keeps the prompt small enough for a small local model.
    private const int MaxProductsInPrompt = 50;
    private const int MaxHistoryTurns = 10;

    private static readonly ChatOptions ChatOptions = new() { Temperature = 0.1f };

    private const string SystemPrompt = """
        You are a friendly customer support assistant for eShop, an online store for outdoor and camping gear.

        Rules:
        - The PRODUCTS list below is the COMPLETE list of everything we sell. No other products exist.
        - Answer ONLY using products from that list. Never invent products, prices, sets or features.
        - When you recommend a product, copy its exact name and price from the list.
        - If the user gives a budget, add up the prices of what you suggest, show the total,
          and make sure the total is NOT more than the budget.
        - If the question cannot be answered from the list, say you don't know and suggest contacting support.
        - If the question is not about shopping or our products, politely say you can only help with eShop products.
        - Keep answers short: at most 5 sentences or a short bullet list.
        """;

    public async Task<SupportResponse> AskAsync(SupportRequest request, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        // 1. Retrieve
        var products = (await productService.GetProductsAsync()).Take(MaxProductsInPrompt).ToList();

        // 2. Augment: system prompt + products, then earlier turns, then the new question.
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, $"{SystemPrompt}\nPRODUCTS:\n{FormatProducts(products)}")
        };

        foreach (var turn in (request.History ?? []).TakeLast(MaxHistoryTurns))
        {
            var role = string.Equals(turn.Role, "assistant", StringComparison.OrdinalIgnoreCase)
                ? ChatRole.Assistant
                : ChatRole.User;
            messages.Add(new ChatMessage(role, turn.Content));
        }

        messages.Add(new ChatMessage(ChatRole.User, request.Question));

        // 3. Generate
        // Low temperature = less "creative", so the model sticks to the product list.
        var response = await chatClient.GetResponseAsync(messages, ChatOptions, cancellationToken);

        stopwatch.Stop();
        logger.LogInformation("AI support answered in {ElapsedMs} ms using {ProductCount} products",
            stopwatch.ElapsedMilliseconds, products.Count);

        return new SupportResponse(response.Text, stopwatch.ElapsedMilliseconds);
    }

    // One line per product, e.g. "- Camping Tent | $99.99 | This tent is perfect for camping trips"
    private static string FormatProducts(IEnumerable<Product> products)
    {
        var builder = new StringBuilder();
        foreach (var product in products)
        {
            builder.Append("- ")
                .Append(product.Name)
                .Append(" | $")
                .Append(product.Price.ToString("0.00", CultureInfo.InvariantCulture))
                .Append(" | ")
                .AppendLine(product.Description);
        }

        return builder.ToString();
    }
}
