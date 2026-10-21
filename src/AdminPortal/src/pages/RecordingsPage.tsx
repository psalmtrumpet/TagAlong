import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { getAllRecordings, fetchRecording, SafetyRecording } from '../lib/api'
import { UserName } from './ReportsPage'

function fmt(d: string) {
  return new Date(d.endsWith('Z') ? d : d + 'Z').toLocaleString('en-NG', { day: '2-digit', month: 'short', hour: '2-digit', minute: '2-digit' })
}

function length(sec: number) {
  return sec < 60 ? `${sec}s` : `${Math.floor(sec / 60)}m ${sec % 60}s`
}

/** Audio riders and drivers recorded during rides, for when there's an issue. */
export default function RecordingsPage() {
  const [rows, setRows] = useState<SafetyRecording[] | null>(null)
  const [urls, setUrls] = useState<Record<string, string>>({})
  const [error, setError] = useState<string | null>(null)

  useEffect(() => { getAllRecordings().then(setRows).catch(e => setError(e.message)) }, [])

  async function play(r: SafetyRecording) {
    const url = await fetchRecording(r.id)
    if (url) setUrls(u => ({ ...u, [r.id]: url }))
    else setError('Could not load that recording.')
  }

  return (
    <div className="p-8">
      <h1 className="text-2xl font-bold text-gray-900">Ride recordings</h1>
      <p className="text-gray-500 text-sm mt-1 mb-6">
        Audio riders and drivers recorded during a ride for their safety. Stored encrypted; kept 30 days (180 if the ride was reported).
        Listening is logged — only play a recording when looking into an issue.
      </p>
      {error && <div className="mb-4 text-sm text-red-600">{error}</div>}
      <div className="card overflow-x-auto">
        <table className="w-full text-sm">
          <thead>
            <tr className="text-xs uppercase tracking-wider text-gray-400 border-b border-gray-100">
              <th className="px-4 py-3 text-left">Recorded</th>
              <th className="px-4 py-3 text-left">By</th>
              <th className="px-4 py-3 text-left">Ride</th>
              <th className="px-4 py-3 text-left">Length</th>
              <th className="px-4 py-3 text-left">Status</th>
              <th className="px-4 py-3 text-left">Listen</th>
            </tr>
          </thead>
          <tbody>
            {rows === null && !error && <tr><td colSpan={6} className="px-4 py-6 text-center text-gray-400">Loading…</td></tr>}
            {rows?.length === 0 && <tr><td colSpan={6} className="px-4 py-6 text-center text-gray-400">No recordings yet.</td></tr>}
            {rows?.map(r => (
              <tr key={r.id} className="border-b border-gray-50">
                <td className="px-4 py-3 text-gray-600">{fmt(r.createdAt)}</td>
                <td className="px-4 py-3"><UserName id={r.userId} /></td>
                <td className="px-4 py-3">
                  {r.conversationId
                    ? <Link to={`/conversations/${r.conversationId}`} className="text-brand-700 hover:underline">Open ride</Link>
                    : '—'}
                </td>
                <td className="px-4 py-3 text-gray-600">{length(r.durationSeconds)}</td>
                <td className="px-4 py-3">
                  {r.flagged
                    ? <span className="text-xs font-medium rounded px-2 py-0.5 bg-red-50 text-red-700">Reported</span>
                    : <span className="text-xs text-gray-400">—</span>}
                </td>
                <td className="px-4 py-3">
                  {urls[r.id]
                    ? <audio controls autoPlay src={urls[r.id]} className="h-8" />
                    : <button onClick={() => play(r)} className="px-3 py-1.5 border border-gray-200 rounded-lg text-xs hover:bg-gray-50">▶ Listen</button>}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  )
}
