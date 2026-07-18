import { Link } from 'react-router-dom'
import { useCart } from '../cart/CartContext'
import { formatMoney } from '../lib/format'

export default function CartDrawer() {
  const { cart, isOpen, close, updateItem, removeItem, setSaved } = useCart()
  if (!isOpen) return null

  const items = cart?.items ?? []
  const active = items.filter((line) => !line.savedForLater)
  const saved = items.filter((line) => line.savedForLater)

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

        {active.length === 0 && saved.length === 0 ? (
          <p className="drawer__empty">Your cart is empty.</p>
        ) : (
          <>
            {active.length === 0 ? (
              <p className="drawer__empty">No items ready for checkout.</p>
            ) : (
              <ul className="drawer__items">
                {active.map((line) => (
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
                      <button type="button" className="link" onClick={() => setSaved(line.variantId, true)}>
                        Save for later
                      </button>
                      <button type="button" className="link" onClick={() => removeItem(line.variantId)}>
                        Remove
                      </button>
                    </div>
                  </li>
                ))}
              </ul>
            )}

            {saved.length > 0 && (
              <section className="drawer__saved">
                <h3>Saved for later</h3>
                <ul className="drawer__items">
                  {saved.map((line) => (
                    <li key={line.variantId} className="drawer__item" data-testid="saved-line">
                      <div className="drawer__item-info">
                        <strong>{line.productTitle}</strong>
                        <div className="muted">{line.variantTitle}</div>
                        <div>{formatMoney(line.unitPrice)}</div>
                      </div>
                      <div className="drawer__item-total">
                        <button type="button" className="link" onClick={() => setSaved(line.variantId, false)}>
                          Move to cart
                        </button>
                        <button type="button" className="link" onClick={() => removeItem(line.variantId)}>
                          Remove
                        </button>
                      </div>
                    </li>
                  ))}
                </ul>
              </section>
            )}

            {active.length > 0 && (
              <div className="drawer__footer">
                <div className="drawer__subtotal">
                  <span>Subtotal</span>
                  <strong>{formatMoney(cart?.subtotal)}</strong>
                </div>
                <Link to="/checkout" className="button primary" onClick={close}>
                  Checkout
                </Link>
              </div>
            )}
          </>
        )}
      </aside>
    </div>
  )
}
