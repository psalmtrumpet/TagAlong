import { useEffect, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { getDrivers, backfillKycPhotos, DriverListItem } from '../lib/api'

const TABS = ['Pending', 'Approved', 'Rejected'] as const

function fmt(d: string) {
  return new Date(d).toLocaleDateString('en-NG', { day: '2-digit', month: 'short', hour: '2-digit', minute: '2-digit' })
}

function CheckBadge({ d }: { d: DriverListItem }) {
  if (!d.documentsChecked) return <span className="text-xs text-gray-400">Not read yet</span>
  if ((d.documentCheckFailures ?? 0) > 0)
    return <span className="text-xs font-medium text-red-600 bg-red-50 rounded px-2 py-0.5">{d.documentCheckFailures} mismatch{d.documentCheckFailures === 1 ? '' : 'es'}</span>
  return <span className="text-xs font-medium text-green-700 bg-green-50 rounded px-2 py-0.5">Matches</span>
}

export default function DriversPage() {
  const navigate = useNavigate()
  const [tab, setTab] = useState<(typeof TABS)[number]>('Pending')
  const [rows, setRows] = useState<DriverListItem[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [backfill, setBackfill] = useState<string | null>(null)

  useEffect(() => {
    setLoading(true)
    setError(null)
    getDrivers(tab).then(setRows).catch(e => setError(e.message)).finally(() => setLoading(false))
  }, [tab])

  async function runBackfill() {
    setBackfill('Saving verification selfies…')
    try {
      const r = await backfillKycPhotos()
      setBackfill(`${r.withPhoto} of ${r.verifiedUsers} verified users now have a selfie on file.`)
    } catch (e) {
      setBackfill((e as Error).message)
    }
  }

  return (
    <div className="p-8">
      <div className="flex items-start justify-between mb-6">
        <div>
          <h1 className="text-2xl font-bold text-gray-900">Drivers</h1>
          <p className="text-gray-500 text-sm mt-1">
            Licence and vehicle documents. Drivers can only offer rides or deliveries once approved.
          </p>
        </div>
        <button
          onClick={runBackfill}
          className="px-4 py-2 border border-gray-200 rounded-lg text-sm font-medium text-gray-700 hover:bg-gray-50"
        >
          Save missing verification selfies
        </button>
      </div>
      {backfill && <div className="mb-4 text-sm text-gray-600">{backfill}</div>}

      <div className="flex gap-2 mb-4">
        {TABS.map(t => (
          <button
            key={t}
            onClick={() => setTab(t)}
            className={`px-3 py-1.5 rounded-lg text-sm font-medium ${tab === t ? 'bg-brand-600 text-black' : 'text-gray-600 hover:bg-gray-100'}`}
          >
            {t === 'Pending' ? 'Waiting for review' : t}
          </button>
        ))}
      </div>

      <div className="card overflow-x-auto">
        <table className="w-full text-sm">
          <thead>
            <tr className="text-xs uppercase tracking-wider text-gray-400 border-b border-gray-100">
              <th className="px-4 py-3 text-left">Driver</th>
              <th className="px-4 py-3 text-left">Vehicle</th>
              <th className="px-4 py-3 text-left">Plate</th>
              <th className="px-4 py-3 text-left">Automatic check</th>
              <th className="px-4 py-3 text-left">Submitted</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-gray-50">
            {loading && <tr><td colSpan={5} className="px-4 py-10 text-center text-gray-400">Loading…</td></tr>}
            {error && <tr><td colSpan={5} className="px-4 py-10 text-center text-red-500">{error}</td></tr>}
            {!loading && !error && rows.length === 0 && (
              <tr><td colSpan={5} className="px-4 py-10 text-center text-gray-400">Nothing here</td></tr>
            )}
            {rows.map(d => (
              <tr key={d.authUserId} onClick={() => navigate(`/drivers/${d.authUserId}`)} className="hover:bg-gray-50 cursor-pointer">
                <td className="px-4 py-3">
                  <div className="font-medium text-gray-900">{d.name}</div>
                  <div className="text-xs text-gray-400">{d.email}{d.isVerified ? ' · NIN verified' : ' · not NIN verified'}</div>
                </td>
                <td className="px-4 py-3 text-gray-600">{d.vehicle}</td>
                <td className="px-4 py-3 font-mono text-gray-900">{d.vehiclePlate}</td>
                <td className="px-4 py-3"><CheckBadge d={d} /></td>
                <td className="px-4 py-3 text-gray-500">{fmt(d.submittedAt)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  )
}
