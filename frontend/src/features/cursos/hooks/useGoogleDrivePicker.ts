import { useCallback, useState } from 'react';
import { validarArchivo } from '../apuntes';

/* Declaración de tipos mínimos para las APIs globales de Google en el navegador */
declare global {
  interface Window {
    gapi: {
      load: (api: string, callbacks: { callback: () => void; onerror?: () => void }) => void;
    };
    google: {
      accounts: {
        oauth2: {
          initTokenClient: (config: {
            client_id: string;
            scope: string;
            callback: (response: { access_token?: string; error?: string }) => void;
            error_callback?: (error: unknown) => void;
          }) => {
            requestAccessToken: (overrideConfig?: { prompt?: string }) => void;
          };
        };
      };
      picker: {
        Action: {
          CANCEL: string;
          PICKED: string;
        };
        DocsView: new (viewId?: string) => {
          setMimeTypes: (mimeTypes: string) => any;
          setMode: (mode: any) => any;
        };
        DocsViewMode: {
          LIST: any;
        };
        PickerBuilder: new () => {
          addView: (view: any) => any;
          setOAuthToken: (token: string) => any;
          setDeveloperKey: (key: string) => any;
          setAppId: (appId: string) => any;
          setCallback: (callback: (data: any) => void) => any;
          build: () => { setVisible: (visible: boolean) => void };
        };
        ViewId: {
          DOCS: string;
        };
      };
    };
  }
}

// Carga asíncrona de scripts en el documento evitando inyecciones duplicadas
function cargarScript(src: string): Promise<void> {
  return new Promise((resolve, reject) => {
    if (document.querySelector(`script[src="${src}"]`)) {
      resolve();
      return;
    }
    const script = document.createElement('script');
    script.src = src;
    script.async = true;
    script.defer = true;
    script.onload = () => resolve();
    script.onerror = () => reject(new Error('No se pudo cargar la librería externa de Google.'));
    document.body.appendChild(script);
  });
}

// Inicializa la librería Google Picker mediante gapi.load
function inicializarGooglePicker(): Promise<void> {
  return new Promise((resolve, reject) => {
    if (window.google?.picker) {
      resolve();
      return;
    }
    cargarScript('https://apis.google.com/js/api.js')
      .then(() => {
        window.gapi.load('picker', {
          callback: resolve,
          onerror: () => reject(new Error('No se pudo inicializar Google Picker.')),
        });
      })
      .catch(reject);
  });
}

// Inicializa Google Identity Services (GIS)
function inicializarGIS(): Promise<void> {
  if (window.google?.accounts?.oauth2) {
    return Promise.resolve();
  }
  return cargarScript('https://accounts.google.com/gsi/client');
}

export type OpcionesAbrirPicker = {
  onArchivoSeleccionado: (archivo: File) => void;
  onError?: (mensaje: string) => void;
};

export function useGoogleDrivePicker() {
  const [isDescargando, setIsDescargando] = useState(false);

  const clientId = import.meta.env.VITE_GOOGLE_CLIENT_ID;
  const apiKey = import.meta.env.VITE_GOOGLE_API_KEY;
  const appId = import.meta.env.VITE_GOOGLE_APP_ID;

  // Feature flag: solo activo si las tres variables requeridas están definidas
  const isGoogleDriveConfigurado = Boolean(clientId && apiKey && appId);

  const abrirPicker = useCallback(
    async ({ onArchivoSeleccionado, onError }: OpcionesAbrirPicker) => {
      if (!isGoogleDriveConfigurado || !clientId || !apiKey || !appId) {
        onError?.('Google Drive no está configurado en las variables de entorno.');
        return;
      }

      try {
        // Cargar ambas librerías en paralelo
        await Promise.all([inicializarGooglePicker(), inicializarGIS()]);

        // Solicitar token de acceso con el scope de menor privilegio (drive.file)
        const token = await new Promise<string>((resolve, reject) => {
          const tokenClient = window.google.accounts.oauth2.initTokenClient({
            client_id: clientId,
            scope: 'https://www.googleapis.com/auth/drive.file',
            callback: (respuesta) => {
              if (respuesta.error) {
                reject(new Error(respuesta.error));
                return;
              }
              if (!respuesta.access_token) {
                reject(new Error('No se obtuvo el token de autorización de Google.'));
                return;
              }
              resolve(respuesta.access_token);
            },
            error_callback: (err) => {
              reject(err);
            },
          });
          tokenClient.requestAccessToken({ prompt: '' });
        });

        // Configuración de vista filtrada únicamente por PDFs
        const vista = new window.google.picker.DocsView(window.google.picker.ViewId.DOCS)
          .setMimeTypes('application/pdf')
          .setMode(window.google.picker.DocsViewMode.LIST);

        // Construcción del Picker con Project Number (setAppId) obligatorio para evitar error 404
        const picker = new window.google.picker.PickerBuilder()
          .addView(vista)
          .setOAuthToken(token)
          .setDeveloperKey(apiKey)
          .setAppId(appId)
          .setCallback(async (data: any) => {
            // Cancelación limpia: no altera el formulario ni lanza errores
            if (data.action === window.google.picker.Action.CANCEL) {
              return;
            }

            if (data.action === window.google.picker.Action.PICKED) {
              const doc = data.docs?.[0];
              if (!doc) return;

              setIsDescargando(true);
              try {
                // Descarga directa del binario mediante la API v3 de Drive
                const respuesta = await fetch(
                  `https://www.googleapis.com/drive/v3/files/${doc.id}?alt=media`,
                  {
                    headers: {
                      Authorization: `Bearer ${token}`,
                    },
                  },
                );

                if (!respuesta.ok) {
                  throw new Error('No se pudo descargar el archivo seleccionado desde Google Drive.');
                }

                const blob = await respuesta.blob();
                const nombreArchivo = doc.name?.toLowerCase().endsWith('.pdf')
                  ? doc.name
                  : `${doc.name || 'apunte'}.pdf`;

                const archivo = new File([blob], nombreArchivo, { type: 'application/pdf' });

                // Validación temprana de reglas de negocio en el cliente (< 10 MB y PDF)
                const errorValidacion = validarArchivo(archivo);
                if (errorValidacion) {
                  onError?.(errorValidacion);
                  return;
                }

                onArchivoSeleccionado(archivo);
              } catch (descargaError) {
                const mensaje =
                  descargaError instanceof Error
                    ? descargaError.message
                    : 'Error al descargar el archivo desde Google Drive.';
                onError?.(mensaje);
              } finally {
                setIsDescargando(false);
              }
            }
          })
          .build();

        picker.setVisible(true);
      } catch (error) {
        const mensaje =
          error instanceof Error
            ? error.message
            : 'No se pudo conectar con el servicio de Google Drive.';
        onError?.(mensaje);
      }
    },
    [isGoogleDriveConfigurado, clientId, apiKey, appId],
  );

  return {
    isGoogleDriveConfigurado,
    isDescargando,
    abrirPicker,
  };
}
