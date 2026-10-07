import { useEffect, useState } from 'react'
import { fetchImage } from '../lib/api'

/** An image that needs the admin's login (licences, selfies) — never a public URL. */
export default function AuthImage({ path, alt, className }: { path: string; alt: string; className?: string }) {
  const [src, setSrc] = useState<string | null>(null)
  const [missing, setMissing] = useState(false)

  useEffect(() => {
    let url: string | null = null
    let cancelled = false
    fetchImage(path).then(u => {
      if (cancelled) { if (u) URL.revokeObjectURL(u); return }
      url = u
      if (u) setSrc(u); else setMissing(true)
    })
    return () => { cancelled = true; if (url) URL.revokeObjectURL(url) }
  }, [path])

  if (missing) return <div className={`${className ?? ''} flex items-center justify-center bg-gray-50 text-xs text-gray-400`}>No image</div>
  if (!src) return <div className={`${className ?? ''} animate-pulse bg-gray-100`} />
  return (
    <a href={src} target="_blank" rel="noreferrer">
      <img src={src} alt={alt} className={className} />
    </a>
  )
}
