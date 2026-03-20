import React from 'react';
import { NavLink, Outlet } from 'react-router-dom';
import { Button } from './Button';
import { useAuth } from '../hooks/useAuth';
import { t } from '../i18n';

function navLinkClassName({ isActive }: { isActive: boolean }): string {
  return [
    'px-3 py-2 rounded block',
    isActive ? 'bg-gray-800' : 'hover:bg-gray-800'
  ].join(' ');
}

export function Layout() {
  const { logout } = useAuth();

  return (
    <div className="flex min-h-screen">
      <aside className="w-64 bg-gray-900 text-white p-4 flex flex-col">
        <div className="text-xl font-semibold mb-6">Fixon</div>

        <nav className="space-y-2">
          <NavLink to="/dashboard" className={navLinkClassName}>
            {t.nav.dashboard}
          </NavLink>
          <NavLink to="/contracts" className={navLinkClassName}>
            {t.nav.contracts}
          </NavLink>
          <NavLink to="/claims" className={navLinkClassName}>
            {t.nav.claims}
          </NavLink>
          <NavLink to="/imports" className={navLinkClassName}>
            {t.nav.imports}
          </NavLink>
          <NavLink to="/members" className={navLinkClassName}>
            {t.nav.members}
          </NavLink>
          <NavLink to="/invites" className={navLinkClassName}>
            {t.nav.invites}
          </NavLink>
        </nav>

        <Button type="button" variant="secondary" className="mt-auto" onClick={() => logout()}>
          {t.nav.logout}
        </Button>
      </aside>

      <main className="flex-1 p-6 bg-gray-50">
        <div className="max-w-6xl mx-auto">
          <Outlet />
        </div>
      </main>
    </div>
  );
}
