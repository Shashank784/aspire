import { useCallback, useEffect, useState, type FormEvent } from 'react'
import { catalogApi, type Product } from '../api'
import { Spinner } from '../components/Spinner'
import { formatPrice, productImage } from '../format'

type ProductForm = { name: string; description: string; price: string; imageUrl: string }

const emptyForm: ProductForm = { name: '', description: '', price: '', imageUrl: '' }

const toForm = (p: Product): ProductForm => ({
  name: p.name,
  description: p.description,
  price: String(p.price),
  imageUrl: p.imageUrl,
})

export function AdminProducts() {
  const [products, setProducts] = useState<Product[] | null>(null)
  const [form, setForm] = useState<ProductForm>(emptyForm)
  const [editingId, setEditingId] = useState<number | null>(null)
  const [message, setMessage] = useState<{ type: 'success' | 'error'; text: string } | null>(null)
  const [busy, setBusy] = useState(false)

  const load = useCallback(() => {
    catalogApi
      .list()
      .then(setProducts)
      .catch(() => setMessage({ type: 'error', text: 'Failed to load products.' }))
  }, [])

  useEffect(load, [load])

  const startEdit = (product: Product) => {
    setEditingId(product.id)
    setForm(toForm(product))
    setMessage(null)
    window.scrollTo({ top: 0, behavior: 'smooth' })
  }

  const cancelEdit = () => {
    setEditingId(null)
    setForm(emptyForm)
  }

  const handleSubmit = async (event: FormEvent) => {
    event.preventDefault()
    const price = Number(form.price)
    if (!Number.isFinite(price) || price < 0) {
      setMessage({ type: 'error', text: 'Please enter a valid price.' })
      return
    }

    const input = { name: form.name.trim(), description: form.description.trim(), price, imageUrl: form.imageUrl.trim() }
    setBusy(true)
    setMessage(null)
    try {
      if (editingId === null) {
        const created = await catalogApi.create(input)
        setMessage({ type: 'success', text: `Created “${created.name}”.` })
      } else {
        await catalogApi.update(editingId, input)
        setMessage({ type: 'success', text: `Saved “${input.name}”.` })
      }
      cancelEdit()
      load()
    } catch (e) {
      setMessage({ type: 'error', text: e instanceof Error ? e.message : 'Failed to save product.' })
    } finally {
      setBusy(false)
    }
  }

  const handleDelete = async (product: Product) => {
    if (!window.confirm(`Delete “${product.name}”? This cannot be undone.`)) return
    try {
      await catalogApi.remove(product.id)
      setMessage({ type: 'success', text: `Deleted “${product.name}”.` })
      if (editingId === product.id) cancelEdit()
      load()
    } catch {
      setMessage({ type: 'error', text: 'Failed to delete product.' })
    }
  }

  const field = (key: keyof ProductForm) => ({
    value: form[key],
    onChange: (e: { target: { value: string } }) => setForm({ ...form, [key]: e.target.value }),
  })

  return (
    <>
      <h1>Admin · Products</h1>

      {message && <div className={`alert alert-${message.type}`}>{message.text}</div>}

      <div className="admin-layout">
        <form className="card form admin-form" onSubmit={handleSubmit}>
          <h2>{editingId === null ? 'Add product' : `Edit product #${editingId}`}</h2>
          <label>
            Name
            <input {...field('name')} required />
          </label>
          <label>
            Description
            <textarea {...field('description')} rows={3} required />
          </label>
          <label>
            Price (₹)
            <input {...field('price')} type="number" min="0" step="0.01" required />
          </label>
          <label>
            Image URL
            <input {...field('imageUrl')} placeholder="https://example.com/my-product.png" required />
            <span className="hint">Paste a full image URL. Bare file names only work for the original seeded products.</span>
          </label>
          <div className="actions">
            <button type="submit" className="btn btn-primary" disabled={busy}>
              {busy ? 'Saving...' : editingId === null ? 'Create product' : 'Save changes'}
            </button>
            {editingId !== null && (
              <button type="button" className="btn btn-ghost" onClick={cancelEdit}>
                Cancel
              </button>
            )}
          </div>
        </form>

        <div className="card admin-list">
          <h2>Existing products</h2>
          {products === null ? (
            <Spinner />
          ) : products.length === 0 ? (
            <p className="muted">No products yet.</p>
          ) : (
            <table className="table">
              <thead>
                <tr>
                  <th />
                  <th>Name</th>
                  <th>Price</th>
                  <th />
                </tr>
              </thead>
              <tbody>
                {products.map((product) => (
                  <tr key={product.id} className={editingId === product.id ? 'row-active' : undefined}>
                    <td>
                      <img className="thumb" src={productImage(product)} alt="" />
                    </td>
                    <td>
                      <div className="product-name">{product.name}</div>
                      <div className="muted small clamp">{product.description}</div>
                    </td>
                    <td>{formatPrice(product.price)}</td>
                    <td className="right nowrap">
                      <button className="btn btn-ghost btn-sm" onClick={() => startEdit(product)}>
                        Edit
                      </button>
                      <button className="btn btn-link-danger btn-sm" onClick={() => handleDelete(product)}>
                        Delete
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </div>
      </div>
    </>
  )
}
