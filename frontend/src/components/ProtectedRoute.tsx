import React from 'react';
import { Navigate } from 'react-router-dom';
import { useAuth } from '../hooks/useAuth';
import { t } from '../i18n';

/**
 * ProtectedRoute:
 * If there is no JWT → redirect to /login.
 * If there is a JWT → render children.
 */
export function ProtectedRoute({ children }: { children: React.ReactNode }) {
  const { isAuthenticated, tenantId, isInitializing } = useAuth();

  if (isInitializing) {
    return <div className="p-4">{t.common.loading}</div>;
  }

  if (!isAuthenticated) {
    return <Navigate to="/login" replace />;
  }

  // Tenant-less JWT support: authenticated but no tenant context -> bootstrap page.
  if (!tenantId) {
    return <Navigate to="/welcome" replace />;
  }

  return <>{children}</>;
}

