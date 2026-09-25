# Caching

There are **3 places** in the project that use caching. All the Redis ones use the same Redis container, named `cache` in the AppHost.

## Where caching is used

| # | Service | What is stored | Where it's stored | Type / library | How long it stays | Why |
|---|---|---|---|---|---|---|
| 1 | **Catalog** | Product list, single product, search results | Memory (L1) + **Redis** (L2) | `HybridCache` | 10 min in Redis, 1 min in memory, 2 min for search | Faster product pages, fewer database calls |
| 2 | **Basket** | Each user's shopping basket (key = user's email) | **Redis** | `IDistributedCache` | Until the order is paid or the basket is deleted (no expiry) | Fast storage for the basket |
| 3 | **Bff** | New login tokens right after a refresh | Memory only | `IMemoryCache` | 1 min | Stops two requests at the same moment from both refreshing the token |

(The old Blazor WebApp also had a Redis output cache, but it was never used, and the WebApp has been deleted.)

## When each cache is cleared

| Cache | Cleared when |
|---|---|
| Catalog products | An admin **adds, edits or deletes** a product, or **uploads an image** (all entries tagged `products` are cleared) |
| Basket | The order is **paid**: the `OrderPaid` event from Service Bus arrives in Basket |
| Bff tokens | After 1 minute, automatically |

## Redis keys you can see in RedisInsight

| Key | Belongs to |
|---|---|
| `catalog:products:all` | Catalog: all products |
| `catalog:products:5` | Catalog: product with id 5 |
| `catalog:search:tent` | Catalog: search results for "tent" |
| `admin@eshop.com` (the user's email) | Basket: that user's basket |

## Key points
- **Basket is not really a "cache".** Redis is the basket's **main storage**; there's no database behind it. If Redis is wiped, baskets are lost. Catalog is a true cache: if Redis is wiped, the data just comes from Postgres again.
- **Cache for reading, database for writing.** Catalog's update and delete skip the cache and use the real database row.
- **L1 vs L2:** L1 (memory) is the fastest but belongs to one instance. L2 (Redis) is shared by all instances.
- **Tags** let you clear many keys at once: one call, `RemoveByTagAsync("products")`, clears the list, the single products, and the searches.

---

# Product images (Blob Storage + Azurite)

## In one line
An admin uploads a product photo; the **file** goes to **blob storage**, and the **database keeps only its name**.

## Before vs now

| | Before | Now |
|---|---|---|
| How an admin adds an image | Pastes a link from the internet | **Uploads a file** from their computer |
| Where the image lives | Someone else's server (Microsoft's GitHub, or any website) | **Our own blob storage** |
| What the database stores | A file name or a full link | The blob name, e.g. `uploads/5f3a9c....png` |
| Risk | Image breaks if the other site removes it | We control it |

## Words to know

| Word | Meaning |
|---|---|
| **Blob** | Any file (image, PDF, video) stored in cloud storage. "Blob" = Binary Large OBject. |
| **Blob Storage** | Azure's service for storing files. AWS's version is S3. |
| **Container** | A folder-like group of blobs. Ours is named `product-images`. |
| **Azurite** | A free **emulator** of Azure Storage that runs in Docker. Same API as the real one, no Azure account needed. |

## The pieces

| Piece | Job |
|---|---|
| **AppHost** | Starts Azurite (`storage`) and creates the `product-images` container. Gives Catalog the connection with `WithReference`. |
| **Catalog: `ProductImageStorage`** | Upload, read and delete files in blob storage |
| **Catalog: endpoints** | `POST /products/{id}/image` (upload, **Admin only**) and `GET /products/images/{name}` (view, **anyone**) |
| **Bff** | Passes both calls through, like every other `/api/...` call. Checks login + CSRF header on the upload. |
| **React: Admin page** | File picker, preview, checks type and size before sending |
| **React: `productImage()`** | Decides which link to use for each product's image |

## Flow 1: Uploading an image

```
Admin page: choose file → click Save
   │
   ├─ 1. React saves the product (name, description, price)      → POST /api/products
   │
   └─ 2. React uploads the file                                     → POST /api/products/{id}/image
           │
           ▼
        Bff: user logged in? CSRF header present? → adds the user's JWT → forwards
           │
           ▼
        Catalog: Admin role? file type allowed? size ≤ 5 MB?
           │
           ├─ saves the file in Azurite as "uploads/<random-guid>.png"
           ├─ saves that name in the product's ImageUrl column (Postgres)
           ├─ clears the product cache (Redis)
           └─ deletes the OLD uploaded image (if there was one)
```

## Flow 2: Showing an image

```
Browser <img src="/api/products/images/uploads/5f3a....png">
   → Bff → Catalog → reads the file from Azurite → sends it back
   (the browser then keeps it in its own cache for a year)
```

## How React picks the image link (`productImage()` in `format.ts`)

| Value in the database | Meaning | Image link used |
|---|---|---|
| `uploads/abc.png` | Uploaded by an admin | `/api/products/images/uploads/abc.png` (our storage) |
| `https://...` | An old full link | That link as it is |
| `product2.png` | One of the 9 seeded products | Microsoft's GitHub sample images |
| empty | No image | A grey "No image" box |

## What happens to the file when...

| Action | Blob storage |
|---|---|
| Product created with an image | New file saved |
| Image replaced on edit | New file saved, **old file deleted** |
| Product edited without choosing a new image | Nothing changes |
| Product deleted | Its file is **deleted** |

## Safety checks (and why)

| Check | Why |
|---|---|
| Only **Admin** can upload | Normal users must not change product images |
| Only **JPEG, PNG, WebP, GIF** | Blocks anything that isn't an image |
| **SVG is blocked** | An SVG file can contain JavaScript, which could attack users (XSS) |
| Max **5 MB** | Stops huge uploads that fill the storage or slow the server |
| Checked in **React and again in Catalog** | React's check is for a nice message; the server check is the real protection (anyone can skip React) |
| File name is a **random GUID** | Never trust the user's file name (it could be `../../hack.exe`); a GUID also means no two files clash |
| `X-Content-Type-Options: nosniff` | Tells the browser to trust our content type and not "guess" |
| Browser cache for 1 year (`immutable`) | Safe because every upload gets a **new** name, so an old name never changes |

## Common questions (good for interviews)

**Why not store the image in the database?**
Databases are expensive and slow for big files. Blob storage is cheap, fast and made for files. The database keeps only a short name.

**Why does Catalog serve the image instead of giving the browser a direct blob link?**
- The storage stays **private**; nobody can list or access other files.
- The database stores no port or server address, which changes locally and differs between local and cloud.
- Later we can put a **CDN** in front without changing the database.

**Why save the product first, then upload the image?**
The upload needs the product's **id**. So: create the product → get the id → upload the image for that id.

**What changes in production (real Azure)?**
Only the AppHost: remove `RunAsEmulator()`, and Aspire creates a real Azure Storage account. The Catalog code stays exactly the same, because Azurite uses the same API.

**What about AWS?**
Same idea with **S3**: swap `ProductImageStorage` to use the S3 SDK. Everything else (endpoints, React) stays the same.

## How to see the stored files
- Aspire dashboard → **storage** resource, or
- **Azure Storage Explorer** (free app) → connect to **Local emulator** → `product-images` container → `uploads/` folder.
