import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { getTrips, AdminTrip } from '../lib/api'
import Pagination from '../components/Pagination'

const TYPES   = ['all', 'Passenger', 'Delivery']
const STATUSES = ['all', 'Scheduled', 'InProgress', 'Completed', 'Cancelled']

function statusBadge(s: string) {
  const map: Record<string, string> = {
    InProgress: 'badge-amber', Completed: 'badge-green',
    Cancelled: 'badge-red', Scheduled: 'badge-blue',
  }
  return <span className={map[s] ?? 'badge-gray'}>{s}</span>
}

function typeBadge(t: string) {
  return t === 'Passenger'
    ? <span className="badge-blue">Passenger</span>
    : <span className="badge-amber">Delivery</span>
}

function fmt(d: string) {
  return new Date(d).toLocaleDateString('en-NG', { day: '2-digit', month: 'short', hour: '2-digit', minute: '2-digit' })
}

export default function TripsPage() {
  const [trips, setTrips] = useState<AdminTrip[]>([])
  const [total, setTotal] = useState(0)
  const [page, setPage]   = useState(1)
  const [type, setType]   = useState('all')
  const [status, setStatus] = useState('all')
  const [loading, setLoading] = useState(true)

  const PAGE_SIZE = 20

  useEffect(() => {
    setLoading(true)
    getTrips(page, PAGE_SIZE, type, status)
      .then(res => { setTrips(res.trips); setTotal(res.total) })
      .catch(() => {})
      .finally(() => setLoading(false))
  }, [page, type, status])

  return (
    <div className="p-8">
      <div className="mb-6">
        <h1 className="text-2xl font-bold text-gray-900">Trips</h1>
        <p className="text-gray-500 text-sm mt-1">{total.toLocaleString()} total trips</p>
      </div>

      <div className="card">
        <div className="flex flex-wrap items-center gap-4 p-4 border-b border-gray-100">
          <div className="flex gap-1">
            {TYPES.map(t => (
              <button key={t} onClick={() => { setType(t); setPage(1) }}
                className={`px-3 py-1.5 rounded-lg text-sm font-medium capitalize transition-colors ${type === t ? 'bg-brand-600 text-white' : 'text-gray-600 hover:bg-gray-100'}`}>
                {t}
              </button>
            ))}
          </div>
          <div className="flex gap-1 ml-4">
            {STATUSES.map(s => (
              <button key={s} onClick={() => { setStatus(s); setPage(1) }}
                className={`px-3 py-1.5 rounded-lg text-sm font-medium capitalize transition-colors ${status === s ? 'bg-gray-800 text-white' : 'text-gray-600 hover:bg-gray-100'}`}>
                {s}
              </button>
            ))}
          </div>
        </div>

        <div className="overflow-x-auto">
          <table className="w-full text-sm">
            <thead>
              <tr className="text-xs uppercase tracking-wider text-gray-400 border-b border-gray-100">
                <th className="px-4 py-3 text-left">Type</th>
                <th className="px-4 py-3 text-left">From</th>
                <th className="px-4 py-3 text-left">To</th>
                <th className="px-4 py-3 text-left">Departure</th>
                <th className="px-4 py-3 text-left">Vehicle</th>
                <th className="px-4 py-3 text-left">Capacity</th>
                <th className="px-4 py-3 text-left">Status</th>
                <th className="px-4 py-3 text-left"></th>
              </tr>
            </thead>
            <tbody>
              {loading ? (
                <tr><td colSpan={8} className="text-center py-12 text-gray-400">Loading…</td></tr>
              ) : trips.length === 0 ? (
                <tr><td colSpan={8} className="text-center py-12 text-gray-400">No trips found</td></tr>
              ) : trips.map(t => (
                <tr key={t.id} className="border-b border-gray-50 hover:bg-gray-50 transition-colors">
                  <td className="px-4 py-3">{typeBadge(t.tripType)}</td>
                  <td className="px-4 py-3 max-w-[180px] truncate text-gray-900" title={t.origin}>{t.origin}</td>
                  <td className="px-4 py-3 max-w-[180px] truncate text-gray-900" title={t.destination}>{t.destination}</td>
                  <td className="px-4 py-3 text-gray-600 whitespace-nowrap">{fmt(t.departureTime)}</td>
                  <td className="px-4 py-3 text-gray-600">{t.vehicleType ?? '—'}</td>
                  <td className="px-4 py-3 text-gray-600">
                    {t.tripType === 'Passenger'
                      ? `${t.currentPassengerCount}/${t.passengerCapacity} pax`
                      : `${t.availableCapacity} kg`}
                  </td>
                  <td className="px-4 py-3">{statusBadge(t.status)}</td>
                  <td className="px-4 py-3">
                    <Link to={`/trips/${t.id}`} className="text-brand-600 hover:underline text-xs font-medium">View →</Link>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>

        <Pagination page={page} pageSize={PAGE_SIZE} total={total} onChange={setPage} />
      </div>
    </div>
  )
}
