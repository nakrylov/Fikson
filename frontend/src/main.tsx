import React from 'react';
import ReactDOM from 'react-dom/client';
import { BrowserRouter } from 'react-router-dom';
import { App } from './App';
import { UserProvider } from './context/UserContext';
import './index.css';

/**
 * Entry point.
 *
 * Order matters:
 * - Router is needed for navigation in pages.
 * - UserProvider holds JWT + claims and is used by ProtectedRoute and pages.
 */
ReactDOM.createRoot(document.getElementById('root') as HTMLElement).render(
  <React.StrictMode>
    <BrowserRouter>
      <UserProvider>
        <App />
      </UserProvider>
    </BrowserRouter>
  </React.StrictMode>
);

