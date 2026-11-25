import { useState, type FormEvent } from 'react'
import { Link } from 'react-router-dom'
import { catalogApi, semanticSearchApi, type Product, type SemanticSearchResponse } from '../api'
import { Spinner } from '../components/Spinner'
import { formatPrice, productImage } from '../format'

// Picked so that keyword search finds nothing, but semantic search finds the right product.
const EXAMPLES = [
  'something to keep me dry',
  'I need light in the dark',
  'cook dinner at the campsite',
  'a place to sleep in the forest',
  'carry my stuff on a long walk',
]

// Side-by-side demo: the normal keyword search (name contains the words) next to
// semantic search (meaning, using vector embeddings). Open to everyone.
export function SmartSearch() {
  const [input, setInput] = useState('')
  const [query, setQuery] = useState('')
  const [keyword, setKeyword] = useState<Product[] | null>(null)
  const [semantic, setSemantic] = useState<SemanticSearchResponse | null>(null)
  const [loading, setLoading] = useState(false)
  const [keywordError, setKeywordError] = useState<string | null>(null)
  const [semanticError, setSemanticError] = useState<string | null>(null)

  const search = async (text: string) => {
    const trimmed = text.trim()
    if (!trimmed || loading) return

    setInput(trimmed)
    setQuery(trimmed)
    setLoading(true)
    setKeyword(null)
    setSemantic(null)
    setKeywordError(null)
    setSemanticError(null)

    // Run both searches at the same time; one failing must not hide the other.
    const [keywordResult, semanticResult] = await Promise.allSettled([
      catalogApi.page(1, 12, trimmed),
      semanticSearchApi.search(trimmed, 5),
    ])

    if (keywordResult.status === 'fulfilled') setKeyword(keywordResult.value.items)
    else setKeywordError('Keyword search failed.')

    if (semanticResult.status === 'fulfilled') setSemantic(semanticResult.value)
    else
      setSemanticError(
        semanticResult.reason instanceof Error
          ? semanticResult.reason.message
          : 'Semantic search failed. The AI model may still be downloading.',
      )

    setLoading(false)
  }

  const handleSubmit = (event: FormEvent) => {
    event.preventDefault()
    void search(input)
  }

  return (
    <div className="smart-search">
      <section className="hero">
        <h1>Smart search</h1>
        <p>
          Describe what you need in your own words. Keyword search only matches product names;
          semantic search understands the meaning.
        </p>
        <form className="search" onSubmit={handleSubmit} role="search">
          <input
            type="search"
            value={input}
            onChange={(e) => setInput(e.target.value)}
            placeholder="e.g. something to keep me dry"
            aria-label="Describe what you are looking for"
            maxLength={500}
          />
          <button className="btn btn-primary" type="submit" disabled={loading || !input.trim()}>
            Search
          </button>
        </form>
        <div className="chat-suggestions">
          {EXAMPLES.map((example) => (
            <button
              key={example}
              type="button"
              className="btn btn-ghost btn-sm"
              onClick={() => void search(example)}
              disabled={loading}
            >
              {example}
            </button>
          ))}
        </div>
      </section>

      {query && (
        <div className="compare">
          <section className="card compare-column">
            <header className="compare-header">
              <h2>Keyword search</h2>
              <span className="muted small">Product name contains “{query}”</span>
            </header>
            {keywordError ? (
              <div className="alert alert-error">{keywordError}</div>
            ) : keyword === null ? (
              <Spinner label="Searching..." />
            ) : keyword.length === 0 ? (
              <p className="muted compare-empty">No products found.</p>
            ) : (
              <ul className="match-list">
                {keyword.map((product) => (
                  <MatchRow key={product.id} product={product} />
                ))}
              </ul>
            )}
          </section>

          <section className="card compare-column">
            <header className="compare-header">
              <h2>Semantic search</h2>
              <span className="muted small">
                Ranked by meaning
                {semantic && ` · ${(semantic.durationMs / 1000).toFixed(2)}s`}
              </span>
            </header>
            {semanticError ? (
              <div className="alert alert-error">{semanticError}</div>
            ) : semantic === null ? (
              <Spinner label="Embedding your text and searching vectors..." />
            ) : semantic.results.length === 0 ? (
              <p className="muted compare-empty">No products found.</p>
            ) : (
              <ul className="match-list">
                {semantic.results.map(({ product, score }) => (
                  <MatchRow key={product.id} product={product} score={score} />
                ))}
              </ul>
            )}
          </section>
        </div>
      )}
    </div>
  )
}

function MatchRow({ product, score }: { product: Product; score?: number }) {
  return (
    <li className="match-row">
      <Link to={`/products/${product.id}`} className="match-image">
        <img src={productImage(product)} alt={product.name} loading="lazy" />
      </Link>
      <div className="match-body">
        <Link to={`/products/${product.id}`} className="product-name">
          {product.name}
        </Link>
        <span className="muted small">{product.description}</span>
        {score !== undefined && (
          <div className="score" title="Cosine similarity: 1 = same meaning, 0 = unrelated">
            <div className="score-bar">
              <div className="score-fill" style={{ width: `${Math.max(0, Math.min(1, score)) * 100}%` }} />
            </div>
            <span className="small">{score.toFixed(2)}</span>
          </div>
        )}
      </div>
      <span className="price">{formatPrice(product.price)}</span>
    </li>
  )
}
