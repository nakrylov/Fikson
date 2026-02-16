import { useUserContext } from '../context/UserContext';

/**
 * Thin wrapper over UserContext.
 * Keeps imports in components/pages clean.
 */
export function useAuth() {
  return useUserContext();
}

