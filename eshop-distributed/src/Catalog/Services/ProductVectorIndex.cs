using System.Globalization;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.VectorData;
using Microsoft.SemanticKernel.Connectors.InMemory;

namespace Catalog.Services;

// Keeps a vector (embedding) for every product in the vector store, and searches it.
//
// Lazy indexing: the first search embeds all products. After that, a search only re-embeds when
// the product list has changed (an admin added, edited or deleted a product), so the vectors never
// go stale. The in-memory store lives as long as this service; a real app would use Qdrant,
// Postgres pgvector, Azure AI Search etc. — only the VectorStore registration would change.
public class ProductVectorIndex(
    IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
    VectorStore vectorStore,
    ILogger<ProductVectorIndex> logger)
{
    private readonly VectorStoreCollection<int, ProductVector> _collection =
        vectorStore.GetCollection<int, ProductVector>("products");

    private readonly SemaphoreSlim _lock = new(1, 1);
    private string? _indexedFingerprint;

    public async Task<List<SemanticSearchResult>> SearchAsync(
        IReadOnlyList<Product> products, string query, int top, CancellationToken cancellationToken)
    {
        await EnsureIndexedAsync(products, cancellationToken);

        // 1. Turn the user's question into a vector with the SAME model used for the products.
        var queryVector = await embeddingGenerator.GenerateVectorAsync(query, cancellationToken: cancellationToken);

        // 2. Ask the vector store for the closest product vectors.
        var results = new List<SemanticSearchResult>();
        await foreach (var match in _collection.SearchAsync(queryVector, top, cancellationToken: cancellationToken))
        {
            var record = match.Record;
            var product = new Product
            {
                Id = record.Id,
                Name = record.Name,
                Description = record.Description,
                Price = record.Price,
                ImageUrl = record.ImageUrl
            };
            results.Add(new SemanticSearchResult(product, Math.Round(match.Score ?? 0, 3)));
        }

        return results;
    }

    private async Task EnsureIndexedAsync(IReadOnlyList<Product> products, CancellationToken cancellationToken)
    {
        var fingerprint = Fingerprint(products);
        if (fingerprint == _indexedFingerprint) return;

        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (fingerprint == _indexedFingerprint) return; // another request just did it

            logger.LogInformation("Embedding {ProductCount} products into the vector store", products.Count);

            // Start clean so deleted products disappear from the results.
            await _collection.EnsureCollectionDeletedAsync(cancellationToken);
            await _collection.EnsureCollectionExistsAsync(cancellationToken);

            // One call embeds all products. Name + description is the text whose meaning we capture.
            var embeddings = await embeddingGenerator.GenerateAsync(
                products.Select(p => $"{p.Name}. {p.Description}"), cancellationToken: cancellationToken);

            var records = products.Select((p, i) => new ProductVector
            {
                Id = p.Id,
                Name = p.Name,
                Description = p.Description,
                Price = p.Price,
                ImageUrl = p.ImageUrl,
                Vector = embeddings[i].Vector
            });

            await _collection.UpsertAsync(records, cancellationToken);
            _indexedFingerprint = fingerprint;
        }
        finally
        {
            _lock.Release();
        }
    }

    // Changes whenever any product is added, removed or edited.
    private static string Fingerprint(IEnumerable<Product> products) =>
        string.Join('\n', products.Select(p =>
            $"{p.Id}|{p.Name}|{p.Description}|{p.Price.ToString(CultureInfo.InvariantCulture)}|{p.ImageUrl}"));
}
