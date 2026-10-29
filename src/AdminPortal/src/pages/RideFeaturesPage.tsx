import { useEffect, useState } from 'react'
import {
  getRideFeatures, createRideFeature, updateRideFeature, deleteRideFeature, RideFeature,
} from '../lib/api'

/** Pictures the app knows. A new key shows as a tick until the app learns it. */
export const ICONS: { key: string; label: string }[] = [
  { key: 'ac', label: '❄️ Air conditioning' },
  { key: 'tinted', label: '🕶️ Tinted windows' },
  { key: 'music', label: '🎵 Music' },
  { key: 'wifi', label: '📶 Wi-Fi' },
  { key: 'charger', label: '🔌 Phone charger' },
  { key: 'water', label: '💧 Water' },
  { key: 'child_seat', label: '👶 Child seat' },
  { key: 'luggage', label: '🧳 Luggage space' },
  { key: 'quiet', label: '🤫 Quiet ride' },
  { key: 'pets', label: '🐾 Pets allowed' },
  { key: 'wheelchair', label: '♿ Wheelchair friendly' },
  { key: 'no_smoking', label: '🚭 No smoking' },
  { key: 'check', label: '✓ Other' },
]

function iconLabel(key: string) {
  return ICONS.find(i => i.key === key)?.label ?? `✓ ${key}`
}

/**
 * What drivers can say their car offers. Drivers tick these when they offer a
 * trip or go available; passengers see them. Changes reach the app straight away.
 */
export default function RideFeaturesPage() {
  const [rows, setRows] = useState<RideFeature[] | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [name, setName] = useState('')
  const [icon, setIcon] = useState('check')
  const [busy, setBusy] = useState(false)
  const [editing, setEditing] = useState<number | null>(null)
  const [editName, setEditName] = useState('')
  const [editIcon, setEditIcon] = useState('check')

  const load = () => getRideFeatures().then(setRows).catch(e => setError(e.message))
  useEffect(() => { load() }, [])

  async function run(action: () => Promise<unknown>) {
    setBusy(true)
    setError(null)
    try {
      await action()
      await load()
    } catch (e) {
      setError((e as Error).message)
    } finally {
      setBusy(false)
    }
  }

  const add = () => run(async () => {
    await createRideFeature({ name: name.trim(), icon })
    setName('')
    setIcon('check')
  })

  const move = (index: number, by: number) => {
    if (!rows) return
    const list = [...rows]
    const [item] = list.splice(index, 1)
    list.splice(index + by, 0, item)
    run(() => Promise.all(list.map((f, i) => f.sortOrder === i + 1 ? null : updateRideFeature(f.id, { sortOrder: i + 1 }))))
  }

  return (
    <div className="p-8 max-w-4xl">
      <h1 className="text-2xl font-bold text-gray-900">Ride features</h1>
      <p className="text-gray-500 text-sm mt-1 mb-6">
        What a driver's car can offer. Drivers tick these (optional) when they offer a trip or go available, and
        passengers see them on the trip and on ride offers. Changes show in the app straight away, with no store update.
        Hide one to stop drivers picking it but keep it on trips that already have it.
      </p>

      {error && <div className="mb-4 text-sm text-red-600">{error}</div>}

      <div className="card p-4 mb-6 flex flex-wrap gap-3 items-end">
        <div className="flex-1 min-w-[200px]">
          <label className="text-xs font-medium text-gray-500">Name</label>
          <input className="input mt-1" placeholder="e.g. Phone charger" value={name} maxLength={60}
            onChange={e => setName(e.target.value)} onKeyDown={e => e.key === 'Enter' && name.trim().length >= 2 && add()} />
        </div>
        <div className="w-56">
          <label className="text-xs font-medium text-gray-500">Picture in the app</label>
          <select className="input mt-1" value={icon} onChange={e => setIcon(e.target.value)}>
            {ICONS.map(i => <option key={i.key} value={i.key}>{i.label}</option>)}
          </select>
        </div>
        <button className="btn-primary" disabled={busy || name.trim().length < 2} onClick={add}>Add feature</button>
      </div>

      <div className="card overflow-x-auto">
        <table className="w-full text-sm">
          <thead>
            <tr className="text-xs uppercase tracking-wider text-gray-400 border-b border-gray-100">
              <th className="px-4 py-3 text-left">Order</th>
              <th className="px-4 py-3 text-left">Name</th>
              <th className="px-4 py-3 text-left">Picture</th>
              <th className="px-4 py-3 text-left">Status</th>
              <th className="px-4 py-3 text-right">Actions</th>
            </tr>
          </thead>
          <tbody>
            {rows === null && !error && <tr><td colSpan={5} className="px-4 py-6 text-center text-gray-400">Loading…</td></tr>}
            {rows?.length === 0 && <tr><td colSpan={5} className="px-4 py-6 text-center text-gray-400">No features yet.</td></tr>}
            {rows?.map((f, i) => (
              <tr key={f.id} className="border-b border-gray-50">
                <td className="px-4 py-3 whitespace-nowrap">
                  <button className="px-1 text-gray-400 hover:text-black disabled:opacity-30" disabled={busy || i === 0} onClick={() => move(i, -1)}>▲</button>
                  <button className="px-1 text-gray-400 hover:text-black disabled:opacity-30" disabled={busy || i === rows.length - 1} onClick={() => move(i, 1)}>▼</button>
                </td>
                <td className="px-4 py-3">
                  {editing === f.id
                    ? <input className="input" value={editName} maxLength={60} onChange={e => setEditName(e.target.value)} />
                    : <span className={f.isActive ? 'font-medium' : 'text-gray-400 line-through'}>{f.name}</span>}
                </td>
                <td className="px-4 py-3 text-gray-600">
                  {editing === f.id
                    ? <select className="input" value={editIcon} onChange={e => setEditIcon(e.target.value)}>
                        {ICONS.map(o => <option key={o.key} value={o.key}>{o.label}</option>)}
                      </select>
                    : iconLabel(f.icon)}
                </td>
                <td className="px-4 py-3">
                  {f.isActive ? <span className="badge-green">Shown</span> : <span className="badge-gray">Hidden</span>}
                </td>
                <td className="px-4 py-3 text-right whitespace-nowrap space-x-2">
                  {editing === f.id ? (
                    <>
                      <button className="btn-primary text-xs" disabled={busy || editName.trim().length < 2}
                        onClick={() => run(async () => { await updateRideFeature(f.id, { name: editName.trim(), icon: editIcon }); setEditing(null) })}>Save</button>
                      <button className="btn-secondary text-xs" onClick={() => setEditing(null)}>Cancel</button>
                    </>
                  ) : (
                    <>
                      <button className="btn-secondary text-xs" disabled={busy}
                        onClick={() => { setEditing(f.id); setEditName(f.name); setEditIcon(f.icon) }}>Edit</button>
                      <button className="btn-secondary text-xs" disabled={busy}
                        onClick={() => run(() => updateRideFeature(f.id, { isActive: !f.isActive }))}>{f.isActive ? 'Hide' : 'Show'}</button>
                      <button className="btn-danger text-xs" disabled={busy}
                        onClick={() => confirm(`Delete "${f.name}"? It disappears from every trip that has it. Hiding keeps it on those.`) &&
                          run(() => deleteRideFeature(f.id))}>Delete</button>
                    </>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  )
}
