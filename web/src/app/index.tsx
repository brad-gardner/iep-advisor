import { AppProvider } from './provider';
import { AppRouter } from './routes';

export function App() {
  return (
    <AppProvider>
      <AppRouter />
    </AppProvider>
  );
}
