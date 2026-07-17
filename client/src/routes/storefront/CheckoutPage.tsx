import { useState, type FormEvent } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { useMutation } from '@tanstack/react-query'
import { useCart } from '../../cart/CartContext'
import { checkout, previewDiscount } from '../../api/cart'
import { ApiError } from '../../api/client'
import { formatMoney } from '../../lib/format'
import type { DiscountPreview } from '../../api/types'

export default function CheckoutPage() {
  const { cart, resetCart } = useCart()
  const navigate = useNavigate()

  const [email, setEmail] = useState('')
  const [name, setName] = useState('')
  const [line1, setLine1] = useState('')
  const [line2, setLine2] = useState('')
  const [city, setCity] = useState('')
  const [region, setRegion] = useState('')
  const [postalCode, setPostalCode] = useState('')
  const [country, setCountry] = useState('US')

  const [discountInput, setDiscountInput] = useState('')
  const [discount, setDiscount] = useState<DiscountPreview | null>(null)

  const applyDiscount = useMutation({
    mutationFn: () => previewDiscount(discountInput.trim(), cart!.subtotal.amount, cart!.subtotal.currency),
    onSuccess: (preview) => setDiscount(preview),
  })

  const mutation = useMutation({
    mutationFn: () =>
      checkout({
        cartToken: cart!.token,
        email,
        discountCode: discount?.valid ? discount.code : undefined,
        shippingAddress: {
          name,
          line1,
          line2: line2 || undefined,
          city,
          region: region || undefined,
          postalCode,
          country,
        },
      }),
    onSuccess: (order) => {
      resetCart()
      navigate(`/order/${order.id}`)
    },
  })

  if (!cart || cart.items.length === 0) {
    return (
      <section>
        <h1>Checkout</h1>
        <p>Your cart is empty.</p>
        <Link to="/">Continue shopping</Link>
      </section>
    )
  }

  function submit(event: FormEvent) {
    event.preventDefault()
    mutation.mutate()
  }

  const errorMessage = mutation.error instanceof ApiError ? mutation.error.message : null

  return (
    <section className="checkout">
      <h1>Checkout</h1>
      <div className="checkout__grid">
        <form className="admin-form" onSubmit={submit}>
          <h2>Contact</h2>
          <label>
            Email
            <input type="email" value={email} onChange={(e) => setEmail(e.target.value)} required />
          </label>

          <h2>Shipping address</h2>
          <label>
            Full name
            <input value={name} onChange={(e) => setName(e.target.value)} required />
          </label>
          <label>
            Address line 1
            <input value={line1} onChange={(e) => setLine1(e.target.value)} required />
          </label>
          <label>
            Address line 2
            <input value={line2} onChange={(e) => setLine2(e.target.value)} />
          </label>
          <label>
            City
            <input value={city} onChange={(e) => setCity(e.target.value)} required />
          </label>
          <label>
            Region / State
            <input value={region} onChange={(e) => setRegion(e.target.value)} />
          </label>
          <label>
            Postal code
            <input value={postalCode} onChange={(e) => setPostalCode(e.target.value)} required />
          </label>
          <label>
            Country (2-letter)
            <input
              value={country}
              onChange={(e) => setCountry(e.target.value.toUpperCase())}
              maxLength={2}
              required
            />
          </label>

          {errorMessage && <p className="error" role="alert">{errorMessage}</p>}

          <button type="submit" className="primary" disabled={mutation.isPending}>
            {mutation.isPending ? 'Placing order…' : `Pay ${formatMoney(cart.subtotal)} + tax & shipping`}
          </button>
        </form>

        <aside className="checkout__summary">
          <h2>Order summary</h2>
          <ul>
            {cart.items.map((line) => (
              <li key={line.variantId} className="checkout__line">
                <span>
                  {line.productTitle} × {line.quantity}
                </span>
                <span>{formatMoney(line.lineTotal)}</span>
              </li>
            ))}
          </ul>
          <div className="checkout__subtotal">
            <span>Subtotal</span>
            <strong>{formatMoney(cart.subtotal)}</strong>
          </div>

          <div className="checkout__discount">
            <label htmlFor="discount-code">Discount code</label>
            <div className="checkout__discount-row">
              <input
                id="discount-code"
                value={discountInput}
                onChange={(e) => setDiscountInput(e.target.value.toUpperCase())}
                placeholder="e.g. WELCOME10"
              />
              <button
                type="button"
                onClick={() => applyDiscount.mutate()}
                disabled={!discountInput.trim() || applyDiscount.isPending}
              >
                Apply
              </button>
            </div>
            {discount && discount.valid && (
              <p className="success" role="status">
                {discount.code} applied — {formatMoney(discount.discount)} off
              </p>
            )}
            {discount && !discount.valid && (
              <p className="error" role="status">{discount.reason ?? 'Invalid code.'}</p>
            )}
          </div>

          <p className="muted">Tax and shipping are calculated at payment.</p>
        </aside>
      </div>
    </section>
  )
}
