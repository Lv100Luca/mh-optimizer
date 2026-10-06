import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { App } from './App';
import { AppStateProvider } from './state';
import './styles.css';

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <AppStateProvider>
      <App />
    </AppStateProvider>
  </StrictMode>,
);
