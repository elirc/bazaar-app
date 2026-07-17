import { Link } from 'react-router-dom'
import { useCart } from '../cart/CartContext'
import { formatMoney } from '../lib/format'

export default function CartDrawer() {
  const { cart, isOpen, close, updateItem, removeItem } = useCart()
  if (!isOpen) return null

  const items = cart?.items ?? []

  return (
    <div className="drawer-overlay" onClick={close}>
      <aside
        className="drawer"
        role="dialog"
        aria-label="Shopping cart"
        onClick={(e) => e.stopPropagation()}
      >
        <header className="drawer__header">
          <h2>Your cart</h2>
          <button type="button" className="drawer__close" onClick={close} aria-label="Close cart">
            ×
          </button>
        </header>

        {items.length === 0 ? (
          <p className="drawer__empty">Your cart is empty.</p>
        ) : (
          <>
            <ul className="drawer__items">
              {items.map((line) => (
                <li key={line.variantId} className="drawer__item" data-testid="cart-line">
                  <div className="drawer__item-info">
                    <strong>{line.productTitle}</strong>
                    <div className="muted">{line.variantTitle}</div>
                    <div>{formatMoney(line.unitPrice)}</div>
                  </div>
                  <div className="drawer__qty">
                    <button
                      type="button"
                      aria-label={`Decrease ${line.sku}`}
                      onClick={() => updateItem(line.variantId, line.quantity - 1)}
                      disabled={line.quantity <= 1}
                    >
                      −
                    </button>
                    <span data-testid="cart-line-qty">{line.quantity}</span>
                    <button
                      type="button"
                      aria-label={`Increase ${line.sku}`}
                      onClick={() => updateItem(line.variantId, line.quantity + 1)}
                      disabled={line.quantity >= line.available}
                    >
                      +
                    </button>
                  </div>
                  <div className="drawer__item-total">
                    {formatMoney(line.lineTotal)}
                    <button type="button" className="link" onClick={() => removeItem(line.variantId)}>
                      Remove
                    </button>
                  </div>
                </li>
              ))}
            </ul>
            <div className="drawer__footer">
              <div className="drawer__subtotal">
                <span>Subtotal</span>
                <strong>{formatMoney(cart?.subtotal)}</strong>
              </div>
              <Link to="/checkout" className="button primary" onClick={close}>
                Checkout
              </Link>
            </div>
          </>
        )}
      </aside>
    </div>
  )
}
