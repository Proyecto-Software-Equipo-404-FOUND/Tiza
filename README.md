# Tiza — Evaluación en Vivo Asistida por IA
> *"Corregí en minutos. Devolvé el mismo día."*

Tiza (PEI) — Plataforma de evaluación institucional en tiempo real asistida por IA. Microservicios en .NET 8, Clean Architecture, React + Vite, SignalR y Docker.

**Tiza** (denominación interna del proyecto: **PEI** - *Plataforma de Evaluación Institucional*) es una solución integral diseñada para instituciones educativas que agiliza la toma y corrección de evaluaciones en tiempo real. Mediante la asistencia de Inteligencia Artificial (MCP / LLMs) con rúbricas personalizadas y la validación final del docente, Tiza reduce el tiempo de corrección y garantiza la devolución pedagógica a los estudiantes el mismo día.

### 🏛️ Arquitectura & Stack Tecnológico
* **Arquitectura:** Microservicios independientes con Clean Architecture (4 capas) y CQRS liviano.
* **Backend:** C# (.NET 8, ASP.NET Core Web API, EF Core).
* **Gateway:** YARP (Yet Another Reverse Proxy).
* **Frontend:** React 18 + TypeScript + Vite + Tailwind CSS + shadcn/ui.
* **Tiempo Real:** SignalR con presencia efímera en Redis.
* **Bases de Datos:** PostgreSQL 16 (una base dedicada por microservicio, con aislamiento multi-tenant por `InstitucionId`).
* **DevOps & Entorno:** Docker & Docker Compose v2.
