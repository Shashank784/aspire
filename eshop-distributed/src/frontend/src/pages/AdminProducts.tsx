import { useCallback, useEffect, useRef, useState, type ChangeEvent, type FormEvent } from 'react'
import { catalogApi, type Product } from '../api'
import { Spinner } from '../components/Spinner'
import { formatPrice, productImage } from '../format'

type ProductForm = { name: string; description: string; price: string }

const emptyForm: ProductForm = { name: '', description: '', price: '' }

const MAX_IMAGE_MB = 5
const ALLOWED_TYPES = ['image/jpeg', 'image/png', 'image/webp', 'image/gif']

export function AdminProducts() {
  const [products, setProducts] = useState<Product[] | null>(null)
  const [form, setForm] = useState<ProductForm>(emptyForm)
  const [editing, setEditing] = useState<Product | null>(null)
  const [imageFile, setImageFile] = useState<File | null>(null)
  const [previewUrl, setPreviewUrl] = useState<string | null>(null)
  const [message, setMessage] = useState<{ type: 'success' | 'error'; text: string } | null>(null)
  const [busy, setBusy] = useState(false)
  const fileInput = useRef<HTMLInputElement>(null)

  const load = useCallback(() => {
    catalogApi
      .list()
      .then(setProducts)
      .catch(() => setMessage({ type: 'error', text: 'Failed to load products.' }))
  }, [])

  useEffect(load, [load])

  // Free the temporary preview URL when it's replaced or the page closes.
  useEffect(() => () => {
    if (previewUrl) URL.revokeObjectURL(previewUrl)
  }, [previewUrl])

  const resetImage = () => {
    setImageFile(null)
    setPreviewUrl(null)
    if (fileInput.current) fileInput.current.value = ''
  }

  const startEdit = (product: Product) => {
    setEditing(product)
    setForm({ name: product.name, description: product.description, price: String(product.price) })
    resetImage()
    setMessage(null)
    window.scrollTo({ top: 0, behavior: 'smooth' })
  }

  const cancelEdit = () => {
    setEditing(null)
    setForm(emptyForm)
    resetImage()
  }

  const handleImageChange = (event: ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0] ?? null
    if (!file) {
      resetImage()
      return
    }
    if (!ALLOWED_TYPES.includes(file.type)) {
      setMessage({ type: 'error', text: 'Please choose a JPEG, PNG, WebP or GIF image.' })
      resetImage()
      return
    }
    if (file.size > MAX_IMAGE_MB * 1024 * 1024) {
      setMessage({ type: 'error', text: `Image must be smaller than ${MAX_IMAGE_MB} MB.` })
      resetImage()
      return
    }
    setMessage(null)
    setImageFile(file)
    setPreviewUrl(URL.createObjectURL(file))
  }

  const handleSubmit = async (event: FormEvent) => {
    event.preventDefault()
    const price = Number(form.price)
    if (!Number.isFinite(price) || price < 0) {
      setMessage({ type: 'error', text: 'Please enter a valid price.' })
      return
    }
    if (!editing && !imageFile) {
      setMessage({ type: 'error', text: 'Please choose an image for the new product.' })
      return
    }

    // The image is uploaded separately (after the product exists), so keep the current one on edit.
    const input = {
      name: form.name.trim(),
      description: form.description.trim(),
      price,
      imageUrl: editing?.imageUrl ?? '',
    }

    setBusy(true)
    setMessage(null)
    try {
      let saved: Product
      if (editing) {
        await catalogApi.update(editing.id, input)
        saved = { ...editing, ...input }
      } else {
        saved = await catalogApi.create(input)
      }

      if (imageFile) {
        await catalogApi.uploadImage(saved.id, imageFile)
      }

      setMessage({ type: 'success', text: `${editing ? 'Saved' : 'Created'} “${saved.name}”.` })
      cancelEdit()
      load()
    } catch (e) {
      setMessage({ type: 'error', text: e instanceof Error ? e.message : 'Failed to save product.' })
      load()
    } finally {
      setBusy(false)
    }
  }

  const handleDelete = async (product: Product) => {
    if (!window.confirm(`Delete “${product.name}”? This cannot be undone.`)) return
    try {
      await catalogApi.remove(product.id)
      setMessage({ type: 'success', text: `Deleted “${product.name}”.` })
      if (editing?.id === product.id) cancelEdit()
      load()
    } catch {
      setMessage({ type: 'error', text: 'Failed to delete product.' })
    }
  }

  const field = (key: keyof ProductForm) => ({
    value: form[key],
    onChange: (e: { target: { value: string } }) => setForm({ ...form, [key]: e.target.value }),
  })

  const shownImage = previewUrl ?? (editing ? productImage(editing) : null)

  return (
    <>
      <h1>Admin · Products</h1>

      {message && <div className={`alert alert-${message.type}`}>{message.text}</div>}

      <div className="admin-layout">
        <form className="card form admin-form" onSubmit={handleSubmit}>
          <h2>{editing ? `Edit product #${editing.id}` : 'Add product'}</h2>
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
            Image
            <input ref={fileInput} type="file" accept={ALLOWED_TYPES.join(',')} onChange={handleImageChange} />
            <span className="hint">
              JPEG, PNG, WebP or GIF, up to {MAX_IMAGE_MB} MB.{editing && ' Leave empty to keep the current image.'}
            </span>
          </label>
          {shownImage && (
            <div className="image-preview">
              <img src={shownImage} alt="Product preview" />
              <span className="muted small">{previewUrl ? 'New image' : 'Current image'}</span>
            </div>
          )}
          <div className="actions">
            <button type="submit" className="btn btn-primary" disabled={busy}>
              {busy ? 'Saving...' : editing ? 'Save changes' : 'Create product'}
            </button>
            {editing && (
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
                  <tr key={product.id} className={editing?.id === product.id ? 'row-active' : undefined}>
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
