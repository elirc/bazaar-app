import { useState, type FormEvent } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { issueGiftCard, listAdminGiftCards } from '../../api/giftcards'
import { ApiError } from '../../api/client'
import { formatMoney } from '../../lib/format'

export default function AdminGiftCardsPage() {
  const queryClient = useQueryClient()
  const [amount, setAmount] = useState('')
  const [code, setCode] = useState('')

  const cardsQuery = useQuery({
    queryKey: ['admin-gift-cards'],
    queryFn: ({ signal }) => listAdminGiftCards(signal),
  })

  const issueMutation = useMutation({
    mutationFn: () => issueGiftCard({ amount: Number(amount), code: code || undefined }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['admin-gift-cards'] })
      setAmount('')
      setCode('')
    },
  })

  function submit(event: FormEvent) {
    event.preventDefault()
    issueMutation.mutate()
  }

  const message = issueMutation.error instanceof ApiError ? issueMutation.error.message : null

  return (
    <section>
      <h1>Gift cards</h1>

      <form className="admin-form inline" onSubmit={submit}>
        <input
          aria-label="Gift card amount"
          type="number"
          step="0.01"
          min="0.01"
          placeholder="Amount"
          value={amount}
          onChange={(e) => setAmount(e.target.value)}
          required
        />
        <input
          aria-label="Gift card code (optional)"
          placeholder="Code (optional)"
          value={code}
          onChange={(e) => setCode(e.target.value.toUpperCase())}
        />
        <button type="submit" className="primary" disabled={issueMutation.isPending}>Issue</button>
      </form>
      {message && <p className="error">{message}</p>}

      {cardsQuery.isLoading && <p>Loading…</p>}
      {cardsQuery.data && (
        <table>
          <thead>
            <tr><th>Code</th><th>Balance</th><th>Issued for</th><th>Active</th></tr>
          </thead>
          <tbody>
            {cardsQuery.data.map((card) => (
              <tr key={card.id} data-testid="gift-card-row">
                <td>{card.code}</td>
                <td>{formatMoney(card.balance)}</td>
                <td>{formatMoney(card.initialBalance)}</td>
                <td>{card.isActive ? 'Yes' : 'No'}</td>
              </tr>
            ))}
            {cardsQuery.data.length === 0 && (
              <tr><td colSpan={4}>No gift cards issued.</td></tr>
            )}
          </tbody>
        </table>
      )}
    </section>
  )
}
