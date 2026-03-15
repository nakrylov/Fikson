import React from 'react';
import { Navigate } from 'react-router-dom';
import { useAuth } from '../hooks/useAuth';

/**
 * ProtectedRoute:
 * If there is no JWT → redirect to /login.
 * If there is a JWT → render children.
 */
export function ProtectedRoute({ children }: { children: React.ReactNode }) {
  const { isAuthenticated, tenantId } = useAuth();

  if (!isAuthenticated) {
    return <Navigate to="/login" replace />;
  }

  // Tenant-less JWT support: authenticated but no tenant context -> bootstrap page.
  if (!tenantId) {
    return <Navigate to="/welcome" replace />;
  }

  return <>{children}</>;
}

