import { useEffect, useState, type FormEvent } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { catalogApi, type PagedResult, type Product } from '../api'
import { AddToBasketButton } from '../components/AddToBasketButton'
import { Pagination } from '../components/Pagination'
import { Spinner } from '../components/Spinner'
import { formatPrice, productImage } from '../format'

const PAGE_SIZE = 12

// Product list + search, one page at a time. Open to everyone — no login needed to browse.
// The search text and page number live in the URL (?q=tent&page=2), so Back/Forward and links work.
export function Products() {
  const [searchParams, setSearchParams] = useSearchParams()
  const query = searchParams.get('q') ?? ''
  const page = Math.max(1, Number(searchParams.get('page')) || 1)
  const [input, setInput] = useState(query)

  // Keep the box in step with the URL (e.g. browser Back after a search).
  useEffect(() => {
    setInput(query)
  }, [query])
  const [result, setResult] = useState<PagedResult<Product> | null>(null)
  const [error, setError] = useState<string | null>(null)
  const products = result?.items ?? null

  useEffect(() => {
    let cancelled = false
    setResult(null)
    setError(null)

    catalogApi
      .page(page, PAGE_SIZE, query)
      .then((data) => {
        if (cancelled) return
        // Page number past the end (old link, or products were deleted): jump to the last page.
        if (data.totalPages > 0 && page > data.totalPages) {
          setSearchParams(buildParams(query, data.totalPages), { replace: true })
          return
        }
        setResult(data)
      })
      .catch(() => !cancelled && setError('We could not load products. Please try again later.'))

    return () => {
      cancelled = true
    }
  }, [page, query, setSearchParams])

  // A new search always starts again from page 1.
  const handleSearch = (event: FormEvent) => {
    event.preventDefault()
    setSearchParams(buildParams(input.trim(), 1))
  }

  const goToPage = (newPage: number) => {
    setSearchParams(buildParams(query, newPage))
    window.scrollTo({ top: 0, behavior: 'smooth' })
  }

  return (
    <>
      <section className="hero">
        <h1>Gear up for the outdoors</h1>
        <p>Tents, backpacks, boots and more — everything you need for your next adventure.</p>
        <form className="search" onSubmit={handleSearch} role="search">
          <input
            type="search"
            value={input}
            onChange={(e) => {
              setInput(e.target.value)
              // Box emptied (backspace or the × button): show all products again.
              if (e.target.value.trim() === '' && query) {
                setSearchParams({})
              }
            }}
            placeholder="Search products..."
            aria-label="Search products"
          />
          <button className="btn btn-primary" type="submit">
            Search
          </button>
        </form>
      </section>

      {query && (
        <div className="results-bar">
          <span>
            Results for <strong>“{query}”</strong>
          </span>
          <button
            className="btn btn-ghost btn-sm"
            onClick={() => {
              setInput('')
              setSearchParams({})
            }}
          >
            Clear search
          </button>
        </div>
      )}

      {error ? (
        <div className="alert alert-error">{error}</div>
      ) : products === null ? (
        <Spinner label="Loading products..." />
      ) : products.length === 0 ? (
        <div className="empty-state">
          <p>No products found.</p>
        </div>
      ) : (
        <div className="product-grid">
          {products.map((product) => (
            <article key={product.id} className="card product-card">
              <Link to={`/products/${product.id}`} className="product-image">
                <img src={productImage(product)} alt={product.name} loading="lazy" />
              </Link>
              <div className="product-body">
                <Link to={`/products/${product.id}`} className="product-name">
                  {product.name}
                </Link>
                <p className="product-description">{product.description}</p>
                <div className="product-footer">
                  <span className="price">{formatPrice(product.price)}</span>
                  <AddToBasketButton product={product} />
                </div>
              </div>
            </article>
          ))}
        </div>
      )}

      {result && result.totalCount > 0 && (
        <>
          <Pagination page={result.page} totalPages={result.totalPages} onChange={goToPage} />
          <p className="pagination-summary muted small">
            Showing {(result.page - 1) * result.pageSize + 1}–{(result.page - 1) * result.pageSize + result.items.length} of{' '}
            {result.totalCount} products
          </p>
        </>
      )}
    </>
  )
}

// URL params for a search term and page; page 1 is left out to keep URLs short.
function buildParams(query: string, page: number): Record<string, string> {
  const params: Record<string, string> = {}
  if (query) params.q = query
  if (page > 1) params.page = String(page)
  return params
}
