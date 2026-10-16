import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import {
  getOpenReports, reviewReport, resolveReport, rejectReport, getRideRecordings, fetchRecording, getUserDetail,
  Report, SafetyRecording,
} from '../lib/api'

const REASONS: Record<string, string> = {
  UnsafeDriving: 'Unsafe driving',
  SafetyConcern: 'Safety concern',
  Overcharging: 'Overcharging',
  Harassment: 'Harassment',
  Fraud: 'Fraud',
  FakeProfile: 'Fake profile',
  Other: 'Other',
}

function fmt(d: string) {
  return new Date(d.endsWith('Z') ? d : d + 'Z').toLocaleString('en-NG', { day: '2-digit', month: 'short', hour: '2-digit', minute: '2-digit' })
}

/** Reports from the app carry the ride as "[Ride <id>]" at the start. */
function rideOf(r: Report): string | null {
  return r.description.match(/\[Ride ([0-9a-f-]{36})\]/i)?.[1] ?? null
}

const nameCache = new Map<string, Promise<string>>()
function nameOf(id: string): Promise<string> {
  if (!nameCache.has(id)) {
    nameCache.set(id, getUserDetail(id).then(u => `${u.firstName} ${u.lastName}`.trim()).catch(() => id.slice(0, 8)))
  }
  return nameCache.get(id)!
}

function UserName({ id }: { id: string | null }) {
  const [name, setName] = useState<string>(id ? id.slice(0, 8) : '—')
  useEffect(() => { if (id) nameOf(id).then(setName) }, [id])
  return id ? <Link to={`/users/${id}`} className="text-brand-700 hover:underline">{name}</Link> : <span>—</span>
}

function Recordings({ rideId }: { rideId: string }) {
  const [items, setItems] = useState<SafetyRecording[] | null>(null)
  const [urls, setUrls] = useState<Record<string, string>>({})
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    getRideRecordings(rideId).then(setItems).catch(e => setError(e.message))
  }, [rideId])

  async function play(r: SafetyRecording) {
    const url = await fetchRecording(r.id)
    if (url) setUrls(u => ({ ...u, [r.id]: url }))
    else setError('Could not load that recording.')
  }

  if (error) return <p className="text-xs text-red-600">{error}</p>
  if (!items) return <p className="text-xs text-gray-400">Loading recordings…</p>
  if (items.length === 0) return <p className="text-xs text-gray-400">No audio was recorded on this ride.</p>
  return (
    <div className="space-y-2">
      {items.map(r => (
        <div key={r.id} className="flex items-center gap-3 text-xs">
          <span className="text-gray-500 w-56">
            By <UserName id={r.userId} /> · {Math.round(r.durationSeconds / 60)} min · {fmt(r.createdAt)}
          </span>
          {urls[r.id]
            ? <audio controls src={urls[r.id]} className="h-8" />
            : <button onClick={() => play(r)} className="px-2 py-1 border border-gray-200 rounded hover:bg-gray-50">▶ Listen</button>}
        </div>
      ))}
      <p className="text-[11px] text-gray-400">Recordings are encrypted; listening is logged. Only use them to look into this report.</p>
    </div>
  )
}

export default function ReportsPage() {
  const [rows, setRows] = useState<Report[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [open, setOpen] = useState<string | null>(null)
  const [note, setNote] = useState('')
  const [busy, setBusy] = useState(false)

  function load() {
    setLoading(true)
    getOpenReports().then(setRows).catch(e => setError(e.message)).finally(() => setLoading(false))
  }
  useEffect(load, [])

  async function act(r: Report, action: 'review' | 'resolve' | 'reject') {
    if (action !== 'review' && !note.trim()) {
      setError('Write what was done (it is saved with the report).')
      return
    }
    setBusy(true)
    setError(null)
    try {
      if (action === 'review') await reviewReport(r.id, note.trim() || 'Looking into it')
      if (action === 'resolve') await resolveReport(r.id, note.trim())
      if (action === 'reject') await rejectReport(r.id, note.trim())
      setNote('')
      setOpen(null)
      load()
    } catch (e) {
      setError((e as Error).message)
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="p-8">
      <h1 className="text-2xl font-bold text-gray-900">Reports</h1>
      <p className="text-gray-500 text-sm mt-1 mb-6">
        Safety and support reports from riders and drivers. Open a report to see the ride, listen to any recording and close it.
      </p>
      {error && <div className="mb-4 text-sm text-red-600">{error}</div>}

      <div className="card overflow-x-auto">
        <table className="w-full text-sm">
          <thead>
            <tr className="text-xs uppercase tracking-wider text-gray-400 border-b border-gray-100">
              <th className="px-4 py-3 text-left">Reason</th>
              <th className="px-4 py-3 text-left">From</th>
              <th className="px-4 py-3 text-left">About</th>
              <th className="px-4 py-3 text-left">Status</th>
              <th className="px-4 py-3 text-left">Sent</th>
            </tr>
          </thead>
          <tbody>
            {loading && <tr><td colSpan={5} className="px-4 py-6 text-center text-gray-400">Loading…</td></tr>}
            {!loading && rows.length === 0 && <tr><td colSpan={5} className="px-4 py-6 text-center text-gray-400">No open reports.</td></tr>}
            {rows.map(r => {
              const ride = rideOf(r)
              const urgent = r.reason === 'SafetyConcern' || r.reason === 'Harassment' || r.reason === 'UnsafeDriving'
              return [
                <tr key={r.id} onClick={() => { setOpen(open === r.id ? null : r.id); setNote('') }}
                    className="border-b border-gray-50 hover:bg-gray-50 cursor-pointer">
                  <td className="px-4 py-3">
                    <span className={`text-xs font-medium rounded px-2 py-0.5 ${urgent ? 'bg-red-50 text-red-700' : 'bg-gray-100 text-gray-700'}`}>
                      {REASONS[r.reason] ?? r.reason}
                    </span>
                  </td>
                  <td className="px-4 py-3"><UserName id={r.reporterId} /></td>
                  <td className="px-4 py-3"><UserName id={r.reportedUserId} /></td>
                  <td className="px-4 py-3 text-gray-600">{r.status === 'UnderReview' ? 'Looking into it' : 'New'}</td>
                  <td className="px-4 py-3 text-gray-500">{fmt(r.createdAt)}</td>
                </tr>,
                open === r.id && (
                  <tr key={r.id + '-d'} className="bg-gray-50 border-b border-gray-100">
                    <td colSpan={5} className="px-4 py-4 space-y-3">
                      <p className="text-gray-800 whitespace-pre-wrap">{r.description.replace(/^\[Ride [0-9a-f-]{36}\]\s*/i, '')}</p>
                      {r.adminNotes && <p className="text-xs text-gray-500">Notes: {r.adminNotes}</p>}
                      {ride && (
                        <>
                          <p className="text-xs">
                            Ride: <Link to={`/conversations/${ride}`} className="text-brand-700 hover:underline">open chat and trip details</Link>
                          </p>
                          <Recordings rideId={ride} />
                        </>
                      )}
                      <textarea value={note} onChange={e => setNote(e.target.value)} rows={2}
                        placeholder="What was done (e.g. called both parties, driver warned, refund issued)…"
                        className="w-full border border-gray-200 rounded-lg p-2 text-sm" />
                      <div className="flex gap-2">
                        {r.status !== 'UnderReview' && (
                          <button disabled={busy} onClick={() => act(r, 'review')}
                            className="px-3 py-1.5 border border-gray-200 rounded-lg text-sm hover:bg-white">Mark as looking into it</button>
                        )}
                        <button disabled={busy} onClick={() => act(r, 'resolve')}
                          className="px-3 py-1.5 bg-brand-600 text-black rounded-lg text-sm font-medium">Resolve</button>
                        <button disabled={busy} onClick={() => act(r, 'reject')}
                          className="px-3 py-1.5 border border-gray-200 rounded-lg text-sm text-gray-600 hover:bg-white">Not valid</button>
                      </div>
                    </td>
                  </tr>
                ),
              ]
            })}
          </tbody>
        </table>
      </div>
    </div>
  )
}
