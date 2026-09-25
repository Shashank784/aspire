import { useEffect, useState, type FormEvent } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { catalogApi, type Product } from '../api'
import { AddToBasketButton } from '../components/AddToBasketButton'
import { Spinner } from '../components/Spinner'
import { formatPrice, productImage } from '../format'

// Product list + search. Open to everyone — no login needed to browse.
export function Products() {
  const [searchParams, setSearchParams] = useSearchParams()
  const query = searchParams.get('q') ?? ''
  const [input, setInput] = useState(query)
  const [products, setProducts] = useState<Product[] | null>(null)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    let cancelled = false
    setProducts(null)
    setError(null)

    const load = query ? catalogApi.search(query) : catalogApi.list()
    load
      .then((result) => !cancelled && setProducts(result))
      .catch(() => !cancelled && setError('We could not load products. Please try again later.'))

    return () => {
      cancelled = true
    }
  }, [query])

  const handleSearch = (event: FormEvent) => {
    event.preventDefault()
    const term = input.trim()
    setSearchParams(term ? { q: term } : {})
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
            onChange={(e) => setInput(e.target.value)}
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
    </>
  )
}
