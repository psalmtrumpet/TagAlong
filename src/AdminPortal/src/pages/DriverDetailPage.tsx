import { useEffect, useState } from 'react'
import { useNavigate, useParams } from 'react-router-dom'
import { approveDriver, getDriver, recheckDriver, rejectDriver, DriverDetail } from '../lib/api'
import AuthImage from '../components/AuthImage'

function Field({ label, value }: { label: string; value?: string | null }) {
  return (
    <div>
      <div className="text-xs font-semibold text-gray-400 uppercase tracking-wider mb-0.5">{label}</div>
      <div className="text-sm text-gray-900">{value || <span className="text-gray-300">—</span>}</div>
    </div>
  )
}

const CHECK_STYLE = {
  pass: 'text-green-700 bg-green-50',
  warn: 'text-amber-700 bg-amber-50',
  fail: 'text-red-700 bg-red-50',
}
const CHECK_LABEL = { pass: 'OK', warn: 'Check', fail: 'Mismatch' }

export default function DriverDetailPage() {
  const { authUserId } = useParams<{ authUserId: string }>()
  const navigate = useNavigate()
  const [d, setD] = useState<DriverDetail | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)
  const [rejecting, setRejecting] = useState(false)
  const [reason, setReason] = useState('')

  useEffect(() => {
    if (authUserId) getDriver(authUserId).then(setD).catch(e => setError(e.message))
  }, [authUserId])

  async function act(fn: () => Promise<unknown>) {
    setBusy(true)
    setError(null)
    try {
      await fn()
      setD(await getDriver(authUserId!))
    } catch (e) {
      setError((e as Error).message)
    } finally {
      setBusy(false)
    }
  }

  if (error && !d) return <div className="p-8 text-red-500">{error}</div>
  if (!d) return <div className="p-8 text-gray-400">Loading…</div>

  const checks = d.documentCheck?.checks ?? []

  return (
    <div className="p-8 max-w-5xl">
      <button onClick={() => navigate('/drivers')} className="text-sm text-gray-500 hover:text-gray-900 mb-4">← Drivers</button>

      <div className="flex items-start justify-between mb-6">
        <div>
          <h1 className="text-2xl font-bold text-gray-900">{d.name}</h1>
          <p className="text-gray-500 text-sm mt-1">
            {d.email} · {d.phoneNumber} · {d.isVerified ? 'NIN verified' : 'not NIN verified'}
          </p>
        </div>
        <span className={`px-3 py-1 rounded-full text-sm font-medium ${
          d.status === 'Approved' ? 'bg-green-50 text-green-700' : d.status === 'Rejected' ? 'bg-red-50 text-red-700' : 'bg-amber-50 text-amber-700'
        }`}>
          {d.status === 'Pending' ? 'Waiting for review' : d.status}
        </span>
      </div>

      {d.status === 'Rejected' && d.rejectionReason && (
        <div className="mb-6 text-sm text-red-700 bg-red-50 rounded-lg px-4 py-3">Rejected: {d.rejectionReason}</div>
      )}
      {error && <div className="mb-4 text-sm text-red-600">{error}</div>}

      <div className="grid grid-cols-1 md:grid-cols-3 gap-6 mb-6">
        <div className="card p-5">
          <div className="text-xs font-semibold text-gray-400 uppercase tracking-wider mb-2">Verification selfie</div>
          <AuthImage path={`/api/users/${d.authUserId}/photo`} alt="Verification selfie" className="w-full h-56 object-cover rounded-lg border border-gray-200" />
        </div>
        <div className="card p-5">
          <div className="text-xs font-semibold text-gray-400 uppercase tracking-wider mb-2">Driver's licence</div>
          <AuthImage path={`/api/admin/users/driver-profiles/${d.authUserId}/license-image`} alt="Licence" className="w-full h-56 object-contain rounded-lg border border-gray-200 bg-gray-50" />
        </div>
        <div className="card p-5">
          <div className="text-xs font-semibold text-gray-400 uppercase tracking-wider mb-2">Vehicle</div>
          {d.hasVehicleImage
            ? <AuthImage path={`/api/admin/users/driver-profiles/${d.authUserId}/vehicle-image`} alt="Vehicle" className="w-full h-56 object-contain rounded-lg border border-gray-200 bg-gray-50" />
            : <div className="w-full h-56 flex items-center justify-center rounded-lg border border-dashed border-gray-200 text-sm text-gray-400">No vehicle photo</div>}
        </div>
      </div>

      <div className="grid grid-cols-1 md:grid-cols-2 gap-6 mb-6">
        <div className="card p-5 grid grid-cols-2 gap-4">
          <Field label="Licence number" value={d.licenseNumber} />
          <Field label="Licence expiry" value={d.licenseExpiry ? new Date(d.licenseExpiry).toLocaleDateString('en-NG') : null} />
          <Field label="Vehicle" value={`${d.vehicleColor} ${d.vehicleMake} ${d.vehicleModel}`} />
          <Field label="Type" value={d.vehicleType} />
          <Field label="Plate" value={d.vehiclePlate} />
          <Field label="Submitted" value={new Date(d.submittedAt).toLocaleString('en-NG')} />
        </div>

        <div className="card p-5">
          <div className="flex items-center justify-between mb-3">
            <div className="text-xs font-semibold text-gray-400 uppercase tracking-wider">Automatic document check</div>
            <button disabled={busy} onClick={() => act(() => recheckDriver(d.authUserId))} className="text-xs text-brand-600 hover:underline disabled:opacity-40">
              Read again
            </button>
          </div>
          {checks.length === 0 ? (
            <p className="text-sm text-gray-400">
              {d.documentCheckedAt ? 'Nothing to report.' : 'Not read yet — it runs a few seconds after upload when the reader is configured.'}
            </p>
          ) : (
            <ul className="space-y-2">
              {checks.map((c, i) => (
                <li key={i} className="flex gap-2 text-sm">
                  <span className={`shrink-0 h-fit text-xs font-medium rounded px-2 py-0.5 ${CHECK_STYLE[c.status]}`}>{CHECK_LABEL[c.status]}</span>
                  <span><span className="font-medium text-gray-900">{c.field}:</span> <span className="text-gray-600">{c.detail}</span></span>
                </li>
              ))}
            </ul>
          )}
          <p className="text-xs text-gray-400 mt-3">Read by AI to help your review — always check the photos yourself.</p>
        </div>
      </div>

      {d.status !== 'Approved' && (
        <div className="flex gap-3">
          <button
            disabled={busy}
            onClick={() => act(() => approveDriver(d.authUserId))}
            className="px-5 py-2.5 bg-green-600 text-white rounded-lg text-sm font-medium hover:bg-green-700 disabled:opacity-40"
          >
            Approve driver
          </button>
          <button
            disabled={busy}
            onClick={() => setRejecting(true)}
            className="px-5 py-2.5 border border-red-200 text-red-600 rounded-lg text-sm font-medium hover:bg-red-50 disabled:opacity-40"
          >
            Reject…
          </button>
        </div>
      )}
      {d.status === 'Approved' && (
        <button disabled={busy} onClick={() => setRejecting(true)} className="px-5 py-2.5 border border-red-200 text-red-600 rounded-lg text-sm font-medium hover:bg-red-50">
          Revoke approval…
        </button>
      )}

      {rejecting && (
        <div className="fixed inset-0 bg-black/30 flex items-center justify-center z-50">
          <div className="bg-white rounded-xl p-6 w-full max-w-md">
            <h2 className="text-lg font-semibold mb-2">Reject driver</h2>
            <p className="text-sm text-gray-500 mb-3">The driver sees this reason in the app and can resubmit.</p>
            <textarea
              className="input w-full h-24"
              placeholder="e.g. Licence photo is blurry — please retake it in good light."
              value={reason}
              onChange={e => setReason(e.target.value)}
            />
            <div className="flex justify-end gap-2 mt-4">
              <button onClick={() => setRejecting(false)} className="px-4 py-2 text-sm text-gray-600">Cancel</button>
              <button
                disabled={!reason.trim() || busy}
                onClick={() => act(() => rejectDriver(d.authUserId, reason)).then(() => { setRejecting(false); setReason('') })}
                className="px-4 py-2 bg-red-600 text-white rounded-lg text-sm font-medium disabled:opacity-40"
              >
                Reject
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  )
}
