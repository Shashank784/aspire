There are **4 places** in the project that use caching. All the Redis ones use the same Redis container, named `cache` in the AppHost.

## Where caching is used

| # | Service | What is stored | Where it's stored | Type / library | How long it stays | Why |
|---|---|---|---|---|---|---|
| 1 | **Catalog** (new) | Product list, single product, search results | Memory (L1) + **Redis** (L2) | `HybridCache` | 10 min in Redis, 1 min in memory, 2 min for search | Faster product pages, fewer database calls |
| 2 | **Basket** | Each user's shopping basket (key = user's email) | **Redis** | `IDistributedCache` | Until the order is paid or the basket is deleted (no expiry) | Fast storage for the basket |
| 3 | **Bff** | New login tokens right after a refresh | Memory only | `IMemoryCache` | 1 min | Stops two requests at the same moment from both refreshing the token |
| 4 | **WebApp** (old Blazor) | Nothing right now | **Redis** | Output cache | — | Switched on, but no page uses it |

## When each cache is cleared

| Cache | Cleared when |
|---|---|
| Catalog products | An admin **adds, edits or deletes** a product (all entries tagged `products` are cleared) |
| Basket | The order is **paid**: the `OrderPaid` event from Service Bus arrives in Basket |
| Bff tokens | After 1 minute, automatically |

## Redis keys you can see in RedisInsight

| Key | Belongs to |
|---|---|
| `catalog:products:all` | Catalog: all products |
| `catalog:products:5` | Catalog: product with id 5 |
| `catalog:search:tent` | Catalog: search results for "tent" |
| `admin@eshop.com` (the user's email) | Basket: that user's basket |

## Key points for your notes
- **Basket is not really a "cache".** Redis is the basket's **main storage**; there's no database behind it. If Redis is wiped, baskets are lost. Catalog is a true cache: if Redis is wiped, the data just comes from Postgres again.
- **Cache for reading, database for writing.** Catalog's update and delete skip the cache and use the real database row.
- **L1 vs L2:** L1 (memory) is the fastest but belongs to one instance. L2 (Redis) is shared by all instances.
- **Tags** let you clear many keys at once: one call, `RemoveByTagAsync("products")`, clears the list, the single products, and the searches.
- **Number 4 does nothing today.** It will go away when we delete the old Blazor WebApp.