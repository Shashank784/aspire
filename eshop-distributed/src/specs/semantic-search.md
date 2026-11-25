# Spec: Semantic Product Search (Vector Embeddings + Vector DB)

## Goal
Customers describe what they need in their own words ("something to keep me dry") and get the
right products, even when no word matches the product name. Keyword search cannot do this.

## Key ideas
- **Embedding**: an AI model turns text into a list of numbers (a vector). Texts with similar
  meaning get vectors that point in a similar direction.
- **Vector database**: stores those vectors and quickly finds the ones closest to a query vector.
- **Cosine similarity** (the score): 1 = same meaning, 0 = unrelated.

## Flow
1. **Index** (first search, and again whenever products change): load all products →
   embed `"{Name}. {Description}"` with `all-minilm` → upsert `ProductVector` records into the vector store.
2. **Search**: embed the user's text with the **same** model → ask the vector store for the top N
   closest vectors → return the products with their scores.

## Architecture
- **AppHost**: `ollama.AddModel("embedding", "all-minilm")` (~45 MB, 384 dimensions), referenced by Catalog.
- **Catalog**:
  - `AddKeyedOllamaApiClient("embedding").AddEmbeddingGenerator()` → `IEmbeddingGenerator<string, Embedding<float>>`
    (Microsoft.Extensions.AI). Keyed so it stays separate from the `chat` client.
  - `VectorStore` = `InMemoryVectorStore` (Microsoft.Extensions.VectorData + `Microsoft.SemanticKernel.Connectors.InMemory`).
    Swap for Qdrant / pgvector / Azure AI Search by changing that one registration.
  - `Models/ProductVector.cs`: record with `[VectorStoreKey]`, `[VectorStoreData]`, `[VectorStoreVector(384)]`.
  - `Services/ProductVectorIndex.cs`: lazy indexing + vector search (singleton).
  - `ProductAIService.SearchProductsAsync`, endpoint `GET /products/ai/search`.
- **BFF**: no change; `/api/products/**` already forwards to Catalog.
- **Frontend**: `Smart search` page (`/smart-search`) shows keyword search vs semantic search side by side.

## API
`GET /products/ai/search?query=...&top=5` (via BFF: `/api/products/ai/search?...`), `top` is 1–20.

```json
{
  "query": "something to keep me dry",
  "results": [ { "product": { "id": 3, "name": "Outdoor Rain Jacket", "price": 49.99, ... }, "score": 0.52 } ],
  "durationMs": 85
}
```
Errors: `400` empty or too long (max 500 chars); `503` model not ready yet.

## Acceptance checks
1. "something to keep me dry" → Outdoor Rain Jacket is in the top results; keyword search finds nothing.
2. "cook dinner at the campsite" → Camping Stove / Camping Cookware near the top.
3. "mountain bike" → results come back, but with clearly lower scores.
4. An admin edits a product → the next search re-embeds (log: "Embedding N products into the vector store").
5. Empty query → `400`. Model still downloading → `503`.

## Out of scope (for now)
Persistent vector DB, hybrid (keyword + vector) ranking, a minimum-score cut-off, using semantic
search to pick products for the AI chat (RAG with retrieval by vector).
