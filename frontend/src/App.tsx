import React from 'react';
import { Navigate, Route, Routes } from 'react-router-dom';
import { ProtectedRoute } from './components/ProtectedRoute';
import { LoginPage } from './pages/LoginPage';
import { ContractsPage } from './pages/ContractsPage';
import { ContractDetailsPage } from './pages/ContractDetailsPage';
import { JoinInvitePage } from './pages/JoinInvitePage';
import { TenantBootstrapPage } from './pages/TenantBootstrapPage';
import { TenantMembersPage } from './pages/TenantMembersPage';
import { TenantInvitesPage } from './pages/TenantInvitesPage';

/**
 * App routes:
 * - /login → LoginPage
 * - /contracts → ContractsPage (protected)
 * - /contract/:id → ContractDetailsPage (protected)
 */
export function App() {
  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />
      <Route path="/join" element={<JoinInvitePage />} />
      <Route path="/welcome" element={<TenantBootstrapPage />} />

      <Route
        path="/contracts"
        element={
          <ProtectedRoute>
            <ContractsPage />
          </ProtectedRoute>
        }
      />
      <Route
        path="/contract/:id"
        element={
          <ProtectedRoute>
            <ContractDetailsPage />
          </ProtectedRoute>
        }
      />
      <Route
        path="/members"
        element={
          <ProtectedRoute>
            <TenantMembersPage />
          </ProtectedRoute>
        }
      />
      <Route
        path="/invites"
        element={
          <ProtectedRoute>
            <TenantInvitesPage />
          </ProtectedRoute>
        }
      />

      <Route path="/" element={<Navigate to="/contracts" replace />} />
      <Route path="*" element={<Navigate to="/contracts" replace />} />
    </Routes>
  );
}

