import React from 'react';
import { Navigate, Route, Routes } from 'react-router-dom';
import { ProtectedRoute } from './components/ProtectedRoute';
import { Layout } from './components/Layout';
import { LoginPage } from './pages/LoginPage';
import { ContractsPage } from './pages/ContractsPage';
import { ContractDetailsPage } from './pages/ContractDetailsPage';
import { JoinInvitePage } from './pages/JoinInvitePage';
import { TenantBootstrapPage } from './pages/TenantBootstrapPage';
import { TenantMembersPage } from './pages/TenantMembersPage';
import { TenantInvitesPage } from './pages/TenantInvitesPage';
import { ClaimsPage } from './pages/ClaimsPage';
import { FactImportPage } from './pages/FactImportPage';
import { DashboardPage } from './pages/DashboardPage';

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
        element={
          <ProtectedRoute>
            <Layout />
          </ProtectedRoute>
        }
      >
        <Route
          path="/dashboard"
          element={<DashboardPage />}
        />
        <Route
          path="/contracts"
          element={<ContractsPage />}
        />
        <Route
          path="/contract/:id"
          element={<ContractDetailsPage />}
        />
        <Route
          path="/members"
          element={<TenantMembersPage />}
        />
        <Route
          path="/invites"
          element={<TenantInvitesPage />}
        />
        <Route
          path="/imports"
          element={<FactImportPage />}
        />
        <Route
          path="/claims"
          element={<ClaimsPage />}
        />
      </Route>

      <Route path="/" element={<Navigate to="/dashboard" replace />} />
      <Route path="*" element={<Navigate to="/dashboard" replace />} />
    </Routes>
  );
}

