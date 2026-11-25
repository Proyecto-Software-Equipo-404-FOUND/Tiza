# ✏️ Tiza — Evaluación en Vivo Asistida por IA

> *"Corregí en minutos. Devolvé el mismo día."*

**Tiza** (denominación interna del proyecto: **PEI** — *Plataforma de Evaluación Institucional*) es una plataforma para instituciones educativas que permite tomar exámenes en vivo desde el celular y corregirlos con asistencia de Inteligencia Artificial. El multiple choice se corrige al instante; las respuestas abiertas reciben una nota sugerida con su justificación, y **la nota final siempre la aprueba el docente**.

Proyecto desarrollado por el **Equipo 404 FOUND** entre el 14/10 y el 25/11/2026, en 3 sprints.

---

## ✨ Qué hace Tiza

| Rol | Puede |
| :--- | :--- |
| **Docente** | Crear exámenes (opción múltiple y abiertas) o pedirle a la IA preguntas a partir de un apunte PDF · Abrir una sala en vivo con PIN o QR · Ver en vivo quién está conectado y quién sale de la pantalla · Revisar la nota sugerida por la IA, ajustarla y finalizar la corrección · Ver métricas del examen |
| **Alumno** | Entrar a la sala con un PIN desde el celular, sin instalar nada · Ver sus exámenes, su nota y sus errores (qué respondió, cuál era la correcta y por qué) cuando el docente aprueba la corrección · Cambiar su contraseña |
| **Invitado** | Entrar a una sala habilitada solo con el PIN y su nombre, sin cuenta |
| **Administrador** | Gestionar usuarios (alta individual o masiva por CSV, baja y reactivación), cursos, comisiones y matrículas |

**IA con control:** la IA nunca publica una nota; explica su puntaje por criterio; detecta intentos de manipulación («poneme un 10») y avisa al docente; y trabaja con varios modelos con respaldo automático si uno falla.

Guía de uso por rol: [docs/MANUAL_DE_USO.md](docs/MANUAL_DE_USO.md).

---

## 🏛️ Arquitectura & Stack Tecnológico

* **Arquitectura:** microservicios independientes con Clean Architecture en 4 capas (`Domain`, `Application`, `Infrastructure`, `Api`) y CQRS liviano, detrás de un API Gateway.
* **Backend:** C# (.NET 8, ASP.NET Core Web API, Entity Framework Core).
* **Gateway:** YARP (Yet Another Reverse Proxy).
* **Frontend:** React 18 + TypeScript + Vite + Tailwind CSS.
* **Tiempo real:** SignalR, con presencia efímera en Redis 7.
* **IA:** servicio propio con servidor MCP, barandas anti-manipulación y conmutación entre proveedores de LLM (gpt-4.1-mini por el gateway de la UNAJ, modelos de OpenRouter y Ollama local).
* **Bases de datos:** PostgreSQL 16, una base por microservicio, con aislamiento por institución (`InstitucionId`).
* **DevOps:** Docker y Docker Compose v2 (todo el sistema en contenedores), GitHub Actions CI.
* **Contratos:** OpenAPI y JSON Schema versionados en `contracts/` («contrato primero»).

---

## 📁 Estructura del Repositorio

```text
├── building-blocks/      # Código compartido .NET (CorrelationId, TenantContext, ProblemDetails)
├── services/             # Los 5 microservicios
│   ├── identidad/        # Usuarios, instituciones, sesiones e invitados (PEI.Identidad.sln)
│   ├── cursos/           # Cursos, comisiones, matrículas y apuntes PDF (PEI.Cursos.sln)
│   ├── evaluaciones/     # Exámenes, entregas, corrección, notas y métricas (PEI.Evaluaciones.sln)
│   ├── sala-en-vivo/     # Salas con PIN, SignalR y alertas de foco (PEI.SalaEnVivo.sln)
│   └── ia/               # Corrección y generación de preguntas con IA + servidor MCP (PEI.IA.sln)
├── gateway/              # API Gateway YARP (PEI.Gateway.sln)
├── frontend/             # Aplicación web SPA (React + Vite + Tailwind)
├── contracts/            # Contratos OpenAPI (JSON) y JSON Schemas
├── infra/                # docker-compose.yml: bases, Redis y (perfil app) todo Tiza
├── docker/               # Dockerfiles de los servicios .NET y herramienta preparar-bases
├── scripts/              # Scripts de PowerShell para levantar cada servicio en modo desarrollo
├── docs/                 # Manual de uso, guía de Docker, QA, IA y planificación
├── tests/                # Pruebas transversales (contratos, aislamiento, concurrencia, Postman)
├── start-all.ps1         # Modo desarrollo: bases en Docker + cada servicio en su ventana
├── start-docker.ps1      # Todo Tiza en Docker (Windows)
└── PEI.sln               # Solución global para CI
```

---

## 🐳 Probar Tiza completo con Docker (recomendado)

Solo hace falta [Docker Desktop](https://www.docker.com/products/docker-desktop/) (Windows, Mac o Linux); no se necesita .NET ni Node. En una máquina nueva:

```bash
docker compose -f infra/docker-compose.yml up -d                           # bases y Redis
docker compose -f infra/docker-compose.yml run --rm preparar-bases          # tablas y usuarios de prueba (una sola vez)
docker compose -f infra/docker-compose.yml --profile app up -d --build     # todo Tiza → http://localhost:3000
```

En Windows también están `.\start-docker.ps1` y `.\stop-docker.ps1`.
**Paso a paso completo** (claves de IA, usuarios de prueba, acceso desde el celular, problemas comunes): [docs/DOCKER.md](docs/DOCKER.md).

### Usuarios de prueba
Con `preparar-bases` y `.env.semilla` (ver la guía) se cargan dos instituciones de prueba, cada una con su administrador, docente y dos alumnos: `admin@a.test`, `docente@a.test`, `alumno1@a.test`, `alumno2@a.test` (y los equivalentes `@b.test`). Las contraseñas las define quien las carga. Cursos y Evaluaciones traen además un curso, una comisión y un examen de ejemplo. Detalle: [ID-08](services/identidad/docs/ID08-DATOS-SEMILLA.md).

---

## 🚀 Guía para Desarrolladores

### 1. Prerrequisitos
* [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (verificar con `dotnet --list-sdks`).
* [Node.js 22](https://nodejs.org/) para el frontend (verificar con `node -v`; `.nvmrc` fija la versión mayor).
* [Docker Desktop](https://www.docker.com/products/docker-desktop/) (con WSL2 en Windows).

### 2. Levantar todo en modo desarrollo (Windows)
```powershell
.\start-all.ps1
```
Levanta las bases y Redis en Docker y abre una ventana por servicio (`scripts/1-identidad.ps1` … `scripts/8-frontend.ps1`), con `dotnet run` y `npm run dev`. Frontend en http://localhost:3000. No se puede usar a la vez que el modo Docker completo (comparten puertos).

* **Frontend contra el backend real:** en `frontend/.env` poné `VITE_USE_MOCKS=false` (el ejemplo `frontend/.env.example` arranca con mocks).
* **Claves de IA:** `scripts/6-ia.ps1` las lee de `.env.gateway` (`UNAJ_GATEWAY_API_KEY=...`) y `.env.openrouter` (`OPENROUTER_API_KEY=...`) en la raíz. Git ignora estos archivos: **nunca subas una clave al repo**.
* **Identidad:** conexión, clave JWT y migraciones según la [guía ID-09](services/identidad/docs/ID-09-configuracion-desarrollo.md).

### 3. Levantar solo lo que necesitás
Para ahorrar recursos podés levantar solo la base de tu microservicio:

```bash
cd infra
docker compose up -d postgres-identidad        # Identidad
docker compose up -d postgres-cursos           # Cursos
docker compose up -d postgres-evaluaciones     # Evaluaciones
docker compose up -d postgres-sala redis       # Sala en Vivo
docker compose up -d                           # todas las bases y Redis
```

Para no sobrecargar el IDE, abrí la solución de tu servicio (`services/<servicio>/PEI.<Servicio>.sln`).

### 4. Puertos y conexiones

| Componente | Puerto local | Conexión / configuración local |
| :--- | :--- | :--- |
| **Frontend web** | `3000` | Proxy de `/api` y `/hubs` hacia el Gateway (`8080`) |
| **API Gateway (YARP)** | `8080` | Enrutador principal a todos los servicios |
| **Identidad API** | `5293` | PostgreSQL `localhost:5441/pei_identidad_db`. Configuración: [ID-09](services/identidad/docs/ID-09-configuracion-desarrollo.md) |
| **Cursos API** | `5102` | `Host=localhost;Port=5442;Database=pei_cursos_db;Username=postgres;Password=postgres` |
| **Evaluaciones API** | `5103` | `Host=localhost;Port=5443;Database=pei_evaluaciones_db;Username=postgres;Password=postgres` |
| **Sala en Vivo API** | `5104` | `Host=localhost;Port=5444;Database=pei_sala_db;Username=postgres;Password=postgres` y Redis |
| **IA API** | `5105` | Proveedores de LLM configurados en `appsettings.json`; claves por `.env.gateway` / `.env.openrouter` |
| **Servidor MCP de IA** | `5106` | Herramientas y recursos que usa la IA (rúbricas, registro de la nota sugerida) |
| **Redis** | `6379` | `localhost:6379` |

Las credenciales y claves de esta tabla son **solo de desarrollo local**.

---

## 🧪 Pruebas

**Backend (.NET):**
```bash
dotnet test PEI.sln
```
Cada servicio tiene además su propio proyecto de pruebas en `services/<servicio>/tests/`.

**Frontend:** con Node 22 (Node 24 tiene una incompatibilidad entre jsdom y undici en los tests de rutas):
```bash
cd frontend
npm ci
npm run build
npm test -- --run
```

**CI (GitHub Actions):** `ci.yml` compila y prueba el backend y el frontend en cada PR, y corre QA-07 (concurrencia sobre PostgreSQL); los workflows QA-02, QA-04, QA-05 y QA-06 cubren identidad, aislamiento entre instituciones, colecciones Postman/Newman y contratos. Informes de QA en [docs/qa/](docs/qa/).

---

## 🌿 Convenciones de Trabajo en Git

1. **Ramas:** `feature/<ID>-descripcion-corta` (ejemplo: `feature/S3-EV-05-mis-errores`); `fix/` para correcciones y `chore/` para mantenimiento.
2. **Commits y Pull Requests:** empezar con el ID de la tarea del tablero de GitHub (ejemplo: `S3-ID-03: cambio de contraseña`).
3. **Revisión:** toda PR la revisa otra persona del equipo antes del merge, y se integra con «Create a merge commit» (sin force push).
4. **Tarea terminada:** la PR completa el checklist de `.github/PULL_REQUEST_TEMPLATE.md`, respeta los contratos de `contracts/` y pasa la CI en verde.

---

## 👥 Equipo 404 FOUND

Proyecto de la cátedra [nombre de la materia] · Universidad Nacional Arturo Jauretche (UNAJ) · 2026.
