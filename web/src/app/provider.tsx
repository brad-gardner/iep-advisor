import { BrowserRouter } from 'react-router-dom';
import { AuthProvider } from '@/features/auth/stores/auth-context';
import { ToastProvider } from '@/components/ui/toast';

interface AppProviderProps {
  children: React.ReactNode;
}

export function AppProvider({ children }: AppProviderProps) {
  // ToastProvider sits above the router so a single toast viewport serves the
  // whole app and survives route changes.
  return (
    <ToastProvider>
      <BrowserRouter>
        <AuthProvider>
          {children}
        </AuthProvider>
      </BrowserRouter>
    </ToastProvider>
  );
}
