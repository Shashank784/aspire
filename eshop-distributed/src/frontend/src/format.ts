import type { Product } from './api'

// Stripe charges in INR, so show INR everywhere.
const inr = new Intl.NumberFormat('en-IN', { style: 'currency', currency: 'INR' })

export const formatPrice = (value: number) => inr.format(value)

export const formatDate = (iso: string | null) =>
  iso ? new Date(iso).toLocaleString('en-IN', { dateStyle: 'medium', timeStyle: 'short' }) : ''

// Seeded products store a bare filename that lives in a demo image repo; products
// created from the Admin page store a full image URL.
export function productImage(product: Product): string {
  if (/^https?:\/\//i.test(product.imageUrl)) {
    return product.imageUrl
  }
  return `https://raw.githubusercontent.com/MicrosoftDocs/mslearn-dotnet-cloudnative/main/dotnet-docker/Products/wwwroot/images/${product.imageUrl}`
}

// Only allow in-app return URLs after login (never "//evil.com" or "https://...").
export function safeReturnUrl(value: string | null): string {
  return value && value.startsWith('/') && !value.startsWith('//') ? value : '/'
}
