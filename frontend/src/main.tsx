import React from 'react';
import ReactDOM from 'react-dom/client';
import App from './App';
import './index.css';

async function iniciar() {
  if (import.meta.env.VITE_USE_MOCKS === 'true') {
    try {
      const { worker } = await import('./mocks/browser');
      await worker.start({ onUnhandledRequest: 'bypass' });
    } catch (error) {
      console.error('No se pudieron iniciar los datos falsos (MSW):', error);
    }
  }

  ReactDOM.createRoot(document.getElementById('root')!).render(
    <React.StrictMode>
      <App />
    </React.StrictMode>,
  );
}

iniciar();
