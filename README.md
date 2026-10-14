# ✏️ Tiza — Evaluación en Vivo Asistida por IA

> *"Corregí en minutos. Devolvé el mismo día."*

**Tiza** (denominación interna del proyecto: **PEI** — *Plataforma de Evaluación Institucional*) es una solución integral diseñada para instituciones educativas que agiliza la toma y corrección de evaluaciones en tiempo real. Mediante la asistencia de Inteligencia Artificial (MCP / LLMs) con rúbricas personalizadas y la validación final del docente, Tiza reduce el tiempo de corrección y garantiza la devolución pedagógica a los estudiantes el mismo día.

---

## 🏛️ Arquitectura & Stack Tecnológico

* **Arquitectura:** Microservicios independientes con Clean Architecture en 4 capas (`Domain`, `Application`, `Infrastructure`, `Api`) y CQRS liviano.
* **Backend:** C# (.NET 8 SDK, ASP.NET Core Web API, Entity Framework Core).
* **Gateway:** YARP (Yet Another Reverse Proxy).
* **Frontend:** React 18 + TypeScript + Vite + Tailwind CSS.
* **Tiempo Real:** SignalR con presencia efímera en Redis 7.
* **Bases de Datos:** PostgreSQL 16 (una base dedicada por microservicio, con aislamiento multi-tenant por `InstitucionId`).
* **DevOps:** Docker & Docker Compose v2, GitHub Actions CI.

---

## 📁 Estructura del Repositorio

```text
├── building-blocks/              # Código compartido .NET (CorrelationId, TenantContext, ProblemDetails)
├── services/                     # Los 5 microservicios
│   ├── identidad/                # Microservicio Identidad (Solución: PEI.Identidad.sln)
│   ├── cursos/                   # Microservicio Cursos (Solución: PEI.Cursos.sln)
│   ├── evaluaciones/             # Microservicio Evaluaciones (Solución: PEI.Evaluaciones.sln)
│   ├── sala-en-vivo/             # Microservicio Sala en Vivo (Solución: PEI.SalaEnVivo.sln)
│   └── ia/                       # Microservicio IA (Solución: PEI.IA.sln)
├── gateway/                      # API Gateway YARP (Solución: PEI.Gateway.sln)
├── frontend/                     # Aplicación web SPA (React + Vite + Tailwind)
├── contracts/                    # Contratos congelados (OpenAPI YAMLs y JSON Schemas)
├── infra/                        # Orquestación Docker (docker-compose.yml y variables)
├── tests/                        # Pruebas transversales (concurrencia y Postman)
└── PEI.sln                       # Solución global para CI
```

---

## 🚀 Guía de Inicio Rápido para Desarrolladores

### 1. Prerrequisitos
* [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (verificar con `dotnet --list-sdks`).
* [Node.js 20 LTS](https://nodejs.org/) (verificar con `node -v`).
* [Docker Desktop](https://www.docker.com/products/docker-desktop/) (con WSL2 en Windows).

### 2. Infraestructura Local (Bases de Datos con Docker)
Para optimizar recursos de tu computadora, **no necesitas levantar todas las bases de datos a la vez**. Sitúate en la carpeta `infra/` y levantá solo la de tu microservicio:

```bash
cd infra

# Si trabajás en Identidad:
docker compose up -d postgres-identidad

# Si trabajás en Cursos:
docker compose up -d postgres-cursos

# Si trabajás en Evaluaciones:
docker compose up -d postgres-evaluaciones

# Si trabajás en Sala en Vivo:
docker compose up -d postgres-sala redis

# Si trabajás en IA:
docker compose up -d postgres-ia

# Para levantar todo el entorno completo (Demos o QA):
docker compose up -d
```

### 3. Tabla de Puertos y Cadenas de Conexión

| Componente | Puerto Local | Cadena de Conexión (`appsettings.Development.json`) |
| :--- | :--- | :--- |
| **API Gateway (YARP)** | `8080` | Enrutador principal a todos los servicios |
| **Identidad API** | `5101` | BD: `Host=localhost;Port=5441;Database=pei_identidad_db;Username=postgres;Password=postgres` |
| **Cursos API** | `5102` | BD: `Host=localhost;Port=5442;Database=pei_cursos_db;Username=postgres;Password=postgres` |
| **Evaluaciones API** | `5103` | BD: `Host=localhost;Port=5443;Database=pei_evaluaciones_db;Username=postgres;Password=postgres` |
| **Sala en Vivo API** | `5104` | BD: `Host=localhost;Port=5444;Database=pei_sala_db;Username=postgres;Password=postgres` |
| **IA API** | `5105` | BD: `Host=localhost;Port=5445;Database=pei_ia_db;Username=postgres;Password=postgres` |
| **Redis** | `6379` | `localhost:6379` |
| **Frontend Web** | `3000` | Proxy automático hacia el Gateway (`8080`) |

### 4. Cómo trabajar en tu Microservicio
Para evitar sobrecargar tu IDE, **abrí únicamente la solución individual de tu servicio**:
* Identidad: `services/identidad/PEI.Identidad.sln`
* Cursos: `services/cursos/PEI.Cursos.sln`
* Evaluaciones: `services/evaluaciones/PEI.Evaluaciones.sln`
* Sala en Vivo: `services/sala-en-vivo/PEI.SalaEnVivo.sln`
* IA: `services/ia/PEI.IA.sln`

---

## 🌿 Convenciones de Trabajo en Git

1. **Ramas:**
   * Nomenclatura: `feature/<ID>-descripcion-corta` (Ejemplo: `feature/ID-03-login`).
2. **Commits y Pull Requests:**
   * Empezar con el ID de la tarea de Jira en español: `ID-03: registro e inicio de sesión`.
3. **Parejas de Revisión Obligatoria:**
   * Identidad $\leftrightarrow$ Cursos
   * Evaluaciones $\leftrightarrow$ Sala en Vivo
   * IA-1 $\leftrightarrow$ IA-2
   * Frontend 1 $\leftrightarrow$ Frontend 2
4. **Criterios de "Tarea Terminada":**
   * Toda PR debe completar el checklist definido en `.github/PULL_REQUEST_TEMPLATE.md` y pasar la CI en verde.
