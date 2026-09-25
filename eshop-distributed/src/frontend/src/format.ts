import type { Product } from './api'

// Stripe charges in INR, so show INR everywhere.
const inr = new Intl.NumberFormat('en-IN', { style: 'currency', currency: 'INR' })

export const formatPrice = (value: number) => inr.format(value)

export const formatDate = (iso: string | null) =>
  iso ? new Date(iso).toLocaleString('en-IN', { dateStyle: 'medium', timeStyle: 'short' }) : ''

const NO_IMAGE =
  'data:image/svg+xml;utf8,' +
  encodeURIComponent(
    '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 120 90"><rect width="120" height="90" fill="#eef0f3"/><text x="60" y="50" font-family="sans-serif" font-size="11" fill="#8a929e" text-anchor="middle">No image</text></svg>',
  )

// Where a product's image comes from, based on what Catalog stored in imageUrl:
//   "uploads/<guid>.png" -> uploaded by an admin, served by Catalog from blob storage
//   "https://..."        -> a full external link
//   "product2.png"       -> an original seeded product, hosted in a demo image repo
export function productImage(product: Product): string {
  const imageUrl = product.imageUrl ?? ''
  if (!imageUrl) {
    return NO_IMAGE
  }
  if (imageUrl.startsWith('uploads/')) {
    return `/api/products/images/${imageUrl}`
  }
  if (/^https?:\/\//i.test(imageUrl)) {
    return imageUrl
  }
  return `https://raw.githubusercontent.com/MicrosoftDocs/mslearn-dotnet-cloudnative/main/dotnet-docker/Products/wwwroot/images/${imageUrl}`
}

// Only allow in-app return URLs after login (never "//evil.com" or "https://...").
export function safeReturnUrl(value: string | null): string {
  return value && value.startsWith('/') && !value.startsWith('//') ? value : '/'
}
