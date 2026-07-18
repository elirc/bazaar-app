import { Link } from 'react-router-dom'
import type { ProductSummary } from '../api/types'
import { formatMoney } from '../lib/format'
import Stars from './Stars'

export default function ProductCard({ product }: { product: ProductSummary }) {
  return (
    <Link to={`/products/${product.slug}`} className="product-card" data-testid="product-card">
      <div className="product-card__media">
        {product.imageUrl ? (
          <img src={product.imageUrl} alt={product.title} loading="lazy" />
        ) : (
          <div className="product-card__placeholder" aria-hidden="true" />
        )}
      </div>
      <div className="product-card__body">
        <h3 className="product-card__title">{product.title}</h3>
        {product.vendor && <p className="product-card__vendor">{product.vendor}</p>}
        {product.reviewCount > 0 && (
          <p className="product-card__rating">
            <Stars rating={product.averageRating} count={product.reviewCount} />
          </p>
        )}
        <p className="product-card__price">{formatMoney(product.priceFrom)}</p>
      </div>
    </Link>
  )
}
