import { useState } from 'react'
import { Link, useLocation, useNavigate } from 'react-router-dom'
import { clearToken, getAdminName, getToken } from '../lib/auth'

const nav = [
  { label: 'Dashboard',      path: '/',              icon: '◈' },
  { label: 'Users',          path: '/users',         icon: '◉' },
  { label: 'Trips',          path: '/trips',         icon: '◎' },
  { label: 'Conversations',  path: '/conversations', icon: '◐' },
  { label: 'Drivers',        path: '/drivers',       icon: '◑' },
  { label: 'Waitlist',       path: '/waitlist',      icon: '◫' },
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
    <div className="flex h-screen overflow-hidden bg-cream">
      {/* Sidebar */}
      <aside className={`flex flex-col bg-white border-r border-gray-200 transition-all duration-200 ${collapsed ? 'w-16' : 'w-56'} flex-shrink-0`}>
        <div className="flex items-center justify-between h-16 px-4 border-b border-gray-100">
          {collapsed ? (
            <img src="/admin/favicon.png" alt="TagAlong" className="h-7 w-7" />
          ) : (
            <div>
              <img src="/admin/logo-wordmark.png" alt="TagAlong" className="h-6 w-auto" />
              <div className="text-[11px] font-medium uppercase tracking-wider text-gray-400 mt-1">Admin Portal</div>
            </div>
          )}
          <button onClick={() => setCollapsed(c => !c)} className="text-gray-400 hover:text-gray-900 ml-auto">
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
                  active ? 'bg-brand-50 text-black font-semibold shadow-[inset_3px_0_0_#F1B01C]' : 'text-gray-600 hover:bg-cream hover:text-black'
                }`}
              >
                <span className="text-base w-5 text-center">{item.icon}</span>
                {!collapsed && <span>{item.label}</span>}
              </Link>
            )
          })}
        </nav>

        <div className="px-2 pb-4 border-t border-gray-100 pt-4">
          {!collapsed && (
            <div className="px-3 pb-3 text-xs text-gray-400 truncate">{adminName}</div>
          )}
          <button
            onClick={logout}
            className="flex items-center gap-3 px-3 py-2.5 rounded-lg text-sm font-medium text-gray-600 hover:bg-cream hover:text-black w-full"
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
