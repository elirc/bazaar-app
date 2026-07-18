import { useState, type FormEvent } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  WEBHOOK_EVENTS,
  createWebhook,
  deleteWebhook,
  listWebhookDeliveries,
  listWebhooks,
} from '../../api/webhooks'
import { ApiError } from '../../api/client'

export default function AdminWebhooksPage() {
  const queryClient = useQueryClient()
  const [url, setUrl] = useState('')
  const [events, setEvents] = useState<string[]>(['order.paid'])

  const subsQuery = useQuery({ queryKey: ['webhooks'], queryFn: ({ signal }) => listWebhooks(signal) })
  const deliveriesQuery = useQuery({
    queryKey: ['webhook-deliveries'],
    queryFn: ({ signal }) => listWebhookDeliveries(undefined, signal),
  })

  const createMutation = useMutation({
    mutationFn: () => createWebhook({ url, events }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['webhooks'] })
      setUrl('')
      setEvents(['order.paid'])
    },
  })

  const deleteMutation = useMutation({
    mutationFn: (id: string) => deleteWebhook(id),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['webhooks'] }),
  })

  function toggleEvent(event: string) {
    setEvents((current) => (current.includes(event) ? current.filter((e) => e !== event) : [...current, event]))
  }

  function submit(e: FormEvent) {
    e.preventDefault()
    createMutation.mutate()
  }

  const message = createMutation.error instanceof ApiError ? createMutation.error.message : null

  return (
    <section className="webhooks-page">
      <h1>Webhooks</h1>

      <form className="admin-form" onSubmit={submit}>
        <label>
          Endpoint URL
          <input type="url" value={url} onChange={(e) => setUrl(e.target.value)} placeholder="https://example.com/hooks" required />
        </label>
        <fieldset>
          <legend>Events</legend>
          <div className="checkbox-group">
            {WEBHOOK_EVENTS.map((event) => (
              <label key={event} className="checkbox">
                <input type="checkbox" checked={events.includes(event)} onChange={() => toggleEvent(event)} />
                {event}
              </label>
            ))}
          </div>
        </fieldset>
        {message && <p className="error">{message}</p>}
        <button type="submit" className="primary" disabled={createMutation.isPending || events.length === 0}>
          Add subscription
        </button>
      </form>

      <h2>Subscriptions</h2>
      {subsQuery.data && (
        <table>
          <thead><tr><th>URL</th><th>Events</th><th>Secret</th><th aria-label="Actions" /></tr></thead>
          <tbody>
            {subsQuery.data.map((sub) => (
              <tr key={sub.id} data-testid="webhook-row">
                <td>{sub.url}</td>
                <td>{sub.events.join(', ')}</td>
                <td><code>{sub.secret}</code></td>
                <td>
                  <button type="button" onClick={() => deleteMutation.mutate(sub.id)} disabled={deleteMutation.isPending}>
                    Delete
                  </button>
                </td>
              </tr>
            ))}
            {subsQuery.data.length === 0 && <tr><td colSpan={4}>No subscriptions.</td></tr>}
          </tbody>
        </table>
      )}

      <h2>Recent deliveries</h2>
      {deliveriesQuery.data && (
        <table>
          <thead><tr><th>Event</th><th>URL</th><th>Result</th><th>Attempts</th></tr></thead>
          <tbody>
            {deliveriesQuery.data.map((delivery) => (
              <tr key={delivery.id} data-testid="delivery-row">
                <td>{delivery.event}</td>
                <td>{delivery.url}</td>
                <td className={delivery.success ? 'success' : 'error'}>
                  {delivery.success ? 'OK' : 'Failed'}{delivery.responseStatus != null ? ` (${delivery.responseStatus})` : ''}
                </td>
                <td>{delivery.attemptCount}</td>
              </tr>
            ))}
            {deliveriesQuery.data.length === 0 && <tr><td colSpan={4}>No deliveries yet.</td></tr>}
          </tbody>
        </table>
      )}
    </section>
  )
}
