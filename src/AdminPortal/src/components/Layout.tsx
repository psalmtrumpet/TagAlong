import { useState } from 'react'
import { Link, useLocation, useNavigate } from 'react-router-dom'
import { clearToken, getAdminName, getToken } from '../lib/auth'

const nav = [
  { label: 'Dashboard',      path: '/',              icon: '◈' },
  { label: 'Users',          path: '/users',         icon: '◉' },
  { label: 'Trips',          path: '/trips',         icon: '◎' },
  { label: 'Conversations',  path: '/conversations', icon: '◐' },
]

export default function Layout({ children }: { children: React.ReactNode }) {
  const { pathname } = useLocation()
  const navigate = useNavigate()
  const [collapsed, setCollapsed] = useState(false)
  const adminName = getAdminName(getToken() ?? '')

  const logout = () => {
    clearToken()
    navigate('/login')
  }

  return (
    <div className="flex h-screen overflow-hidden bg-gray-50">
      {/* Sidebar */}
      <aside className={`flex flex-col bg-brand-600 text-white transition-all duration-200 ${collapsed ? 'w-16' : 'w-56'} flex-shrink-0`}>
        <div className="flex items-center justify-between h-16 px-4 border-b border-brand-700">
          {!collapsed && (
            <div>
              <div className="font-bold text-sm tracking-wide">TagAlong</div>
              <div className="text-xs text-green-200 opacity-75">Admin Portal</div>
            </div>
          )}
          <button onClick={() => setCollapsed(c => !c)} className="text-white/70 hover:text-white ml-auto">
            {collapsed ? '›' : '‹'}
          </button>
        </div>

        <nav className="flex-1 px-2 py-4 space-y-1">
          {nav.map(item => {
            const active = item.path === '/' ? pathname === '/' : pathname.startsWith(item.path)
            return (
              <Link
                key={item.path}
                to={item.path}
                className={`flex items-center gap-3 px-3 py-2.5 rounded-lg text-sm font-medium transition-colors ${
                  active ? 'bg-brand-700 text-white' : 'text-green-100 hover:bg-brand-700/60 hover:text-white'
                }`}
              >
                <span className="text-base w-5 text-center">{item.icon}</span>
                {!collapsed && <span>{item.label}</span>}
              </Link>
            )
          })}
        </nav>

        <div className="px-2 pb-4 border-t border-brand-700 pt-4">
          {!collapsed && (
            <div className="px-3 pb-3 text-xs text-green-200 opacity-75 truncate">{adminName}</div>
          )}
          <button
            onClick={logout}
            className="flex items-center gap-3 px-3 py-2.5 rounded-lg text-sm font-medium text-green-100 hover:bg-brand-700/60 hover:text-white w-full"
          >
            <span className="text-base w-5 text-center">⬡</span>
            {!collapsed && <span>Sign out</span>}
          </button>
        </div>
      </aside>

      {/* Main */}
      <main className="flex-1 overflow-y-auto">
        {children}
      </main>
    </div>
  )
}
