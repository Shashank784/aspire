using Microsoft.Extensions.VectorData;

namespace Catalog.Models;

// One product as it is stored in the vector database.
// The attributes tell the vector store which property is the key, which are plain data,
// and which one holds the embedding (the list of numbers that captures the meaning).
public class ProductVector
{
    [VectorStoreKey]
    public int Id { get; set; }

    [VectorStoreData]
    public string Name { get; set; } = default!;

    [VectorStoreData]
    public string Description { get; set; } = default!;

    [VectorStoreData]
    public decimal Price { get; set; }

    [VectorStoreData]
    public string ImageUrl { get; set; } = default!;

    // all-minilm returns 384 numbers per text. Cosine similarity: 1 = same meaning, 0 = unrelated.
    [VectorStoreVector(Dimensions: 384, DistanceFunction = DistanceFunction.CosineSimilarity)]
    public ReadOnlyMemory<float> Vector { get; set; }
}
