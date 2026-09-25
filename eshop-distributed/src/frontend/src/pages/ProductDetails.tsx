import { useEffect, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { ApiError, catalogApi, type Product } from '../api'
import { AddToBasketButton } from '../components/AddToBasketButton'
import { Spinner } from '../components/Spinner'
import { formatPrice, productImage } from '../format'

export function ProductDetails() {
  const { id } = useParams()
  const [product, setProduct] = useState<Product | null>(null)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    setProduct(null)
    setError(null)
    catalogApi
      .get(Number(id))
      .then(setProduct)
      .catch((e) => setError(e instanceof ApiError && e.status === 404 ? 'Product not found.' : 'Could not load this product.'))
  }, [id])

  if (error) {
    return (
      <div className="empty-state">
        <p>{error}</p>
        <Link to="/" className="btn btn-primary">
          Back to shop
        </Link>
      </div>
    )
  }

  if (!product) {
    return <Spinner />
  }

  return (
    <>
      <Link to="/" className="back-link">
        ← Back to shop
      </Link>
      <div className="details card">
        <div className="details-image">
          <img src={productImage(product)} alt={product.name} />
        </div>
        <div className="details-body">
          <h1>{product.name}</h1>
          <p className="price price-lg">{formatPrice(product.price)}</p>
          <p className="details-description">{product.description}</p>
          <AddToBasketButton product={product} size="lg" />
        </div>
      </div>
    </>
  )
}
