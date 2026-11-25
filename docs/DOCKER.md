# Tiza completo en Docker

Con Docker se levanta **todo Tiza con un solo comando**: las 5 bases PostgreSQL, Redis, los 6 servicios .NET
(Identidad, Cursos, Evaluaciones, Sala en Vivo, IA y su servidor MCP), el API Gateway y el frontend.
No hace falta tener .NET ni Node instalados: solo Docker Desktop. Funciona en Windows, Mac (Intel y Apple Silicon) y Linux.

> Es la configuración de **desarrollo y demo** (`ASPNETCORE_ENVIRONMENT=Development`, claves de desarrollo
> de `appsettings.Development.json`). No es una configuración de producción: para publicarlo en un servidor
> hay que usar claves propias y secretas, modo Production, HTTPS y backups.

## Primera vez, paso a paso (máquina nueva)

### 1. Instalar Docker Desktop
Bajalo de [docker.com](https://www.docker.com/products/docker-desktop/) (en Mac, la versión para tu chip) y abrilo.
En *Settings → Resources* asigná **al menos 6 GB de memoria**: son 14 contenedores.

### 2. Clonar el repo
```bash
git clone https://github.com/Proyecto-Software-Equipo-404-FOUND/Tiza.git
cd Tiza
```

### 3. Claves de la IA (opcional)
Creá en la raíz del repo uno o los dos archivos, cada uno con una sola línea. Git los ignora: **nunca los subas
ni los compartas por chat o mail**.

| Archivo | Contenido | Para qué |
|---|---|---|
| `.env.gateway` | `UNAJ_GATEWAY_API_KEY=...` | Modelo principal (gpt-4.1-mini) |
| `.env.openrouter` | `OPENROUTER_API_KEY=...` | Modelos de respaldo |

Sin claves, Tiza funciona igual, pero la corrección y la generación de preguntas con IA no van a responder.

### 4. Usuarios de prueba (opcional, recomendado)
Copiá el ejemplo y poné una contraseña propia (8 caracteres o más) para cada usuario
(admin@a.test, docente@a.test, alumno1@a.test, ...):

```bash
cp .env.semilla.example .env.semilla          # Windows PowerShell: Copy-Item .env.semilla.example .env.semilla
```
### Google Drive (opcional)
Para subir apuntes desde Google Drive, creá `frontend/.env.local` (git lo ignora) con
`VITE_GOOGLE_CLIENT_ID`, `VITE_GOOGLE_API_KEY` y `VITE_GOOGLE_APP_ID` del proyecto de Google Cloud
(ver [S3-CU-05](../services/cursos/docs/S3-CU-05-GOOGLE-DRIVE-PICKER.md)). Solo funciona entrando por
`http://localhost:3000` y con cuentas de Gmail registradas como usuarios de prueba. Sin ese archivo,
la opción de Drive simplemente no aparece.

### 5. Preparar las bases (una sola vez)
```bash
docker compose -f infra/docker-compose.yml up -d
docker compose -f infra/docker-compose.yml run --rm preparar-bases
```
Crea las tablas de Identidad, Cursos y Evaluaciones (Sala se migra sola al arrancar) y, si existe `.env.semilla`,
carga los usuarios de prueba de [ID-08](../services/identidad/docs/ID08-DATOS-SEMILLA.md).
Se puede correr más de una vez: lo que ya está aplicado no se toca.

### 6. Levantar Tiza
```bash
docker compose -f infra/docker-compose.yml --profile app up -d --build
```
En Windows también podés usar `.\start-docker.ps1`, que hace lo mismo y muestra la dirección para el celular.
La primera vez tarda varios minutos porque compila todas las imágenes.

### 7. Entrar
- En la compu: **http://localhost:3000**
- Desde el celular (misma red Wi-Fi): `http://IP-DE-LA-COMPU:3000`. La IP se ve con `ipconfig` (Windows)
  o `ipconfig getifaddr en0` (Mac). Si no carga, revisá el firewall de la compu.

Con los usuarios de prueba cargados, entrá como `docente@a.test`: Cursos y Evaluaciones ya traen un curso,
una comisión y un examen de ejemplo.

## Uso diario

| Para | Comando |
|---|---|
| Levantar (reconstruye solo lo que cambió) | `docker compose -f infra/docker-compose.yml --profile app up -d --build` · Windows: `.\start-docker.ps1` |
| Levantar sin reconstruir | `docker compose -f infra/docker-compose.yml --profile app up -d` · Windows: `.\start-docker.ps1 -SinBuild` |
| Apagar (los datos se conservan) | `docker compose -f infra/docker-compose.yml --profile app down` · Windows: `.\stop-docker.ps1` |
| Ver el estado | `docker compose -f infra/docker-compose.yml --profile app ps` |
| Ver los logs de un servicio | `docker compose -f infra/docker-compose.yml --profile app logs -f identidad` |

Si en Windows PowerShell no deja ejecutar los scripts («no está firmado digitalmente»), corré una vez
`Unblock-File .\start-docker.ps1, .\stop-docker.ps1`.

## Qué se levanta

| Contenedor | Qué es | Puerto en la compu |
|---|---|---|
| `pei-frontend` | Frontend (nginx), reenvía `/api` y `/hubs` al gateway | **3000** |
| `pei-gateway` | API Gateway (YARP), con `appsettings.Docker.json` | 8080 |
| `pei-identidad` | Identidad | 5293 |
| `pei-cursos` | Cursos (los PDF quedan en el volumen `cursos_storage`) | 5102 |
| `pei-evaluaciones` | Evaluaciones | 5103 |
| `pei-sala-en-vivo` | Sala en Vivo (SignalR) | 5104 |
| `pei-ia` | Servicio de IA (lee las claves al arrancar) | 5105 |
| `pei-mcp-ia` | Servidor MCP de la IA | 5106 |
| `pei-db-*`, `pei-redis` | Bases y Redis | 5441–5445, 6379 |

Son los mismos puertos que el modo de desarrollo con `start-all.ps1`, así que **no se pueden usar los dos modos
a la vez**. Los dos usan las mismas bases y volúmenes: pasar de uno a otro no pierde datos.

Si cambiás una clave de IA: `docker compose -f infra/docker-compose.yml --profile app up -d ia`.

## Problemas comunes

| Síntoma | Qué hacer |
|---|---|
| `port is already allocated` | Hay algo usando el puerto: cerrá las ventanas de `start-all.ps1` (o el `npm run dev`). |
| Un servicio se reinicia solo | Mirá sus logs: `docker compose -f infra/docker-compose.yml --profile app logs --tail 40 <servicio>`. |
| Login con «Algo salió mal» / 502 | Revisá `logs gateway`: tiene que decir `Proxying to http://identidad:8080/...`. |
| No hay usuarios para entrar | Faltó el paso 4 o el 5: completá `.env.semilla` y corré `preparar-bases`. |
| La sala en vivo no conecta | Revisá `logs gateway` y `logs sala-en-vivo`; el frontend se abre por el puerto 3000. |
| Cambié código y no se ve | Volvé a levantar con `--build`. En el navegador, `Ctrl+F5` (Mac: `Cmd+Shift+R`). |
| Quiero borrar todo y empezar de cero | `docker compose -f infra/docker-compose.yml --profile app down -v` (**borra las bases**) y repetí desde el paso 5. |
