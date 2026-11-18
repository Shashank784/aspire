# Spec: AI Support Assistant

## Goal
Customers can ask questions in plain English about our products and get short, correct answers
based on our real catalog (names, prices, descriptions). No login needed.

## Approach (simple RAG)
1. **Retrieve** all products from the Catalog (Redis cache → Postgres).
2. **Augment** the system prompt with the product list.
3. **Generate** the answer with a local LLM (Ollama `llama3.2`, 3b) through `IChatClient`
   (Microsoft.Extensions.AI).

## Architecture
- **AppHost**: `AddOllama("ollama")` container + `AddModel("chat", "llama3.2")`, referenced by Catalog.
- **Catalog**: `AddOllamaApiClient("chat").AddChatClient()`, `ProductAIService`, `ProductAIEndpoints`.
- **BFF**: no change; `/api/products/**` already forwards to Catalog.

## API
| Method | URL (Catalog) | URL (via BFF) | Body |
|---|---|---|---|
| GET | `/products/ai/support?question=...` | `/api/products/ai/support?question=...` | – |
| POST | `/products/ai/chat` | `/api/products/ai/chat` (+ header `X-CSRF: 1`) | `{ "question": "...", "history": [ { "role": "user\|assistant", "content": "..." } ] }` |

Response `200`:
```json
{ "answer": "The Camping Stove ($49.99) ...", "durationMs": 4210 }
```
Errors: `400` empty or too long question (max 500 chars); `503` model not ready yet.

## Acceptance checks
1. "What can I use to cook while camping?" → mentions **Camping Stove** and/or **Camping Cookware** with prices.
2. "Do you sell mountain bikes?" → says it doesn't know / we don't sell it (no invented product).
3. Off-topic question → polite refusal.
4. Follow-up "Is it good for rain?" with history about a jacket → talks about the **Outdoor Rain Jacket**.
5. Empty question → `400`.
6. Model still downloading → `503` with a friendly message (not a crash).

## UI
React page **Ask AI** (`/support`, `frontend/src/pages/Support.tsx`): chat bubbles, suggested
questions, "Thinking…" while waiting, answer time, Clear button. Calls `POST /api/products/ai/chat`
through the BFF with the full chat history.

## Out of scope (for now)
Vector database / embeddings, semantic search, streaming answers, function calling.
