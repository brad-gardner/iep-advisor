import { BrowserRouter } from 'react-router-dom';
import { AuthProvider } from '@/features/auth/stores/auth-context';

interface AppProviderProps {
  children: React.ReactNode;
}

export function AppProvider({ children }: AppProviderProps) {
  return (
    <BrowserRouter>
      <AuthProvider>
        {children}
      </AuthProvider>
    </BrowserRouter>
  );
}
