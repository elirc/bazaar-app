import { useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { useMutation, useQuery } from '@tanstack/react-query'
import { getStorefrontProduct } from '../../api/catalog'
import { addToDefaultWishlist } from '../../api/wishlist'
import { formatMoney } from '../../lib/format'
import { useCart } from '../../cart/CartContext'
import { useAuth } from '../../auth/AuthContext'
import Stars from '../../components/Stars'
import ProductReviews from '../../components/ProductReviews'

export default function ProductDetailPage() {
  const { slug } = useParams<{ slug: string }>()
  const { addItem } = useCart()
  const { isAuthenticated } = useAuth()
  const [selectedVariantId, setSelectedVariantId] = useState<string | null>(null)
  const [adding, setAdding] = useState(false)

  const wishlistMutation = useMutation({
    mutationFn: (variantId: string) => addToDefaultWishlist(variantId),
  })

  const { data: product, isLoading, isError, error } = useQuery({
    queryKey: ['storefront-product', slug],
    queryFn: ({ signal }) => getStorefrontProduct(slug!, signal),
    enabled: Boolean(slug),
  })

  if (isLoading) return <p>Loading…</p>
  if (isError) {
    const status = (error as { status?: number } | null)?.status
    return (
      <div>
        <p className="error">{status === 404 ? 'Product not found.' : 'Could not load this product.'}</p>
        <Link to="/">← Back to shop</Link>
      </div>
    )
  }
  if (!product) return null

  const selectedVariant =
    product.variants.find((v) => v.id === selectedVariantId) ?? product.variants[0]

  async function handleAddToCart() {
    if (!selectedVariant) return
    setAdding(true)
    try {
      await addItem(selectedVariant.id, 1)
    } finally {
      setAdding(false)
    }
  }

  return (
    <article className="product-detail">
      <Link to="/" className="product-detail__back">← Back to shop</Link>
      <div className="product-detail__grid">
        <div className="product-detail__media">
          {product.images[0] ? (
            <img src={product.images[0].url} alt={product.images[0].altText ?? product.title} />
          ) : (
            <div className="product-card__placeholder" aria-hidden="true" />
          )}
        </div>

        <div className="product-detail__info">
          <h1>{product.title}</h1>
          {product.vendor && <p className="product-detail__vendor">{product.vendor}</p>}
          <p className="product-detail__rating">
            <Stars rating={product.averageRating} count={product.reviewCount} />
          </p>
          <p className="product-detail__price">{formatMoney(selectedVariant?.price)}</p>
          <p className="product-detail__description">{product.description}</p>

          {product.variants.length > 1 && (
            <label className="product-detail__variant">
              Variant
              <select
                value={selectedVariant?.id ?? ''}
                onChange={(e) => setSelectedVariantId(e.target.value)}
              >
                {product.variants.map((v) => (
                  <option key={v.id} value={v.id}>
                    {v.title} — {formatMoney(v.price)}
                  </option>
                ))}
              </select>
            </label>
          )}

          <p className="product-detail__stock" data-testid="availability">
            {selectedVariant && selectedVariant.available > 0
              ? `${selectedVariant.available} in stock`
              : 'Out of stock'}
          </p>

          <div className="product-detail__actions">
            <button
              type="button"
              className="primary product-detail__add"
              onClick={handleAddToCart}
              disabled={adding || !selectedVariant || selectedVariant.available <= 0}
            >
              {adding ? 'Adding…' : 'Add to cart'}
            </button>
            {isAuthenticated && selectedVariant && (
              <button
                type="button"
                onClick={() => wishlistMutation.mutate(selectedVariant.id)}
                disabled={wishlistMutation.isPending}
              >
                {wishlistMutation.isSuccess ? 'Added to wishlist ✓' : 'Add to wishlist'}
              </button>
            )}
          </div>

          {selectedVariant && selectedVariant.options.length > 0 && (
            <ul className="product-detail__options">
              {selectedVariant.options.map((o) => (
                <li key={o.name}>
                  <strong>{o.name}:</strong> {o.value}
                </li>
              ))}
            </ul>
          )}
        </div>
      </div>

      <ProductReviews slug={product.slug} />
    </article>
  )
}
