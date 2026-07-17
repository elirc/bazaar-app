import { useQuery } from '@tanstack/react-query'
import { getHealth } from '../../api/health'

export default function HomePage() {
  const { data, isLoading, isError } = useQuery({
    queryKey: ['health'],
    queryFn: ({ signal }) => getHealth(signal),
  })

  return (
    <section className="home">
      <h1>Welcome to Bazaar</h1>
      <p>Browse our catalog of finely sourced goods.</p>
      <p className="api-status" data-testid="api-status">
        {isLoading && <span>Checking API…</span>}
        {isError && <span>API unavailable</span>}
        {data && <span>API status: {data.status}</span>}
      </p>
    </section>
  )
}
