import { useEffect, useState } from 'react'
import { useParams, useNavigate } from 'react-router-dom'
import { getTrip, AdminTrip } from '../lib/api'

function Field({ label, value }: { label: string; value?: string | number | null }) {
  return (
    <div>
      <div className="text-xs font-semibold text-gray-400 uppercase tracking-wider mb-0.5">{label}</div>
      <div className="text-sm text-gray-900">{value ?? <span className="text-gray-300">—</span>}</div>
    </div>
  )
}

function fmt(d?: string | null) {
  if (!d) return null
  return new Date(d).toLocaleDateString('en-NG', { day: '2-digit', month: 'short', year: 'numeric', hour: '2-digit', minute: '2-digit' })
}

export default function TripDetailPage() {
  const { id } = useParams<{ id: string }>()
  const navigate = useNavigate()
  const [trip, setTrip] = useState<AdminTrip | null>(null)
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    if (!id) return
    getTrip(id).then(setTrip).catch(() => {}).finally(() => setLoading(false))
  }, [id])

  if (loading) return <div className="p-8 text-gray-400">Loading…</div>
  if (!trip)   return <div className="p-8 text-red-500">Trip not found</div>

  const mapUrl = `https://www.openstreetmap.org/export/embed.html?bbox=${
    Math.min(trip.originLongitude, trip.destinationLongitude) - 0.05
  }%2C${
    Math.min(trip.originLatitude, trip.destinationLatitude) - 0.05
  }%2C${
    Math.max(trip.originLongitude, trip.destinationLongitude) + 0.05
  }%2C${
    Math.max(trip.originLatitude, trip.destinationLatitude) + 0.05
  }&layer=mapnik`

  return (
    <div className="p-8 max-w-5xl">
      <button onClick={() => navigate(-1)} className="text-sm text-gray-400 hover:text-gray-700 mb-4">← Back</button>

      <div className="flex items-start justify-between mb-6">
        <div>
          <h1 className="text-2xl font-bold text-gray-900">
            {trip.tripType === 'Passenger' ? 'Passenger Trip' : 'Delivery Trip'}
          </h1>
          <div className="text-sm text-gray-400 font-mono mt-1">{trip.id}</div>
        </div>
        <div className="flex items-center gap-2">
          <span className={`badge-${
            trip.status === 'InProgress' ? 'amber' :
            trip.status === 'Completed'  ? 'green' :
            trip.status === 'Cancelled'  ? 'red' : 'blue'
          }`}>{trip.status}</span>
        </div>
      </div>

      <div className="grid grid-cols-1 md:grid-cols-2 gap-4 mb-6">
        {/* Route info */}
        <div className="card p-5">
          <div className="text-xs font-semibold text-gray-400 uppercase tracking-wider mb-4">Route</div>
          <div className="space-y-4">
            <div>
              <div className="text-xs text-gray-400 mb-0.5">Origin</div>
              <div className="text-sm font-medium text-gray-900">{trip.origin}</div>
              <div className="text-xs text-gray-400 font-mono">{trip.originLatitude.toFixed(5)}, {trip.originLongitude.toFixed(5)}</div>
            </div>
            <div className="border-l-2 border-brand-200 ml-2 h-4"></div>
            <div>
              <div className="text-xs text-gray-400 mb-0.5">Destination</div>
              <div className="text-sm font-medium text-gray-900">{trip.destination}</div>
              <div className="text-xs text-gray-400 font-mono">{trip.destinationLatitude.toFixed(5)}, {trip.destinationLongitude.toFixed(5)}</div>
            </div>
          </div>
        </div>

        {/* Trip details */}
        <div className="card p-5">
          <div className="text-xs font-semibold text-gray-400 uppercase tracking-wider mb-4">Details</div>
          <div className="grid grid-cols-2 gap-3">
            <Field label="Traveler ID"  value={trip.travelerId} />
            <Field label="Departure"    value={fmt(trip.departureTime) ?? undefined} />
            <Field label="Vehicle"      value={trip.vehicleType} />
            <Field label="Plate"        value={trip.vehiclePlateNumber} />
            {trip.tripType === 'Passenger' ? (
              <>
                <Field label="Pax Capacity" value={trip.passengerCapacity} />
                <Field label="Pax On Board" value={trip.currentPassengerCount} />
              </>
            ) : (
              <>
                <Field label="Capacity (kg)"  value={trip.availableCapacity} />
                <Field label="Notes"          value={trip.notes} />
              </>
            )}
            <Field label="Created"      value={fmt(trip.createdAt) ?? undefined} />
          </div>
        </div>
      </div>

      {/* Live location */}
      {trip.currentLatitude && trip.currentLongitude && (
        <div className="card p-5 mb-4">
          <div className="text-xs font-semibold text-gray-400 uppercase tracking-wider mb-2">Last Known Location</div>
          <div className="text-sm text-gray-900 font-mono">{trip.currentLatitude.toFixed(5)}, {trip.currentLongitude.toFixed(5)}</div>
        </div>
      )}

      {/* Map */}
      <div className="card overflow-hidden mb-4">
        <div className="text-xs font-semibold text-gray-400 uppercase tracking-wider p-4 pb-0">Route Map</div>
        <iframe
          src={mapUrl}
          className="w-full h-72 border-0"
          title="Route map"
          loading="lazy"
        />
      </div>

      {trip.notes && (
        <div className="card p-5">
          <div className="text-xs font-semibold text-gray-400 uppercase tracking-wider mb-2">Notes</div>
          <div className="text-sm text-gray-700">{trip.notes}</div>
        </div>
      )}
    </div>
  )
}
