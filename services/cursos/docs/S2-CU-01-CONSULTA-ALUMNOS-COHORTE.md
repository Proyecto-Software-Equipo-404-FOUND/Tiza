# CONTRATO E INTEGRACIÓN INTER-SERVICIO: S2-CU-01 (FASE 1)
> **Microservicio:** PEI.Cursos  
> **Target Consumer:** PEI.SalaEnVivo (S2-SA-01 / Validación de Alumno al Unirse por PIN)  
> **Fecha:** 2026-10-29  
> **Estado:** Implementado & Verificado (89 Unit Tests PASS)

---

## 1. FLUJO DE EJECUCIÓN END-TO-END

La petición HTTP entrante es interceptada por `TenantContextMiddleware`, extrayendo `InstitucionId` y validando la firma HMAC-SHA256 del JWT contra Identidad. `MatriculacionesController` delega la ejecución mediante `IMediator.SendAsync` a `GetMatriculacionesByCohorteQueryHandler`, el cual consulta `ICohorteRepository` para verificar la existencia y estado operativo (`Activo == true`) de la comisión bajo el tenant actual, retornando `null` (`404 Not Found` RFC 9457) ante inconsistencias para evitar ataques de enumeración.

Si la cohorte es válida, el handler invoca `IMatriculacionRepository.GetByCohorteIdAsync(cohorteId, soloActivas)`, ejecutando una consulta `AsNoTracking()` en PostgreSQL (`pei_cursos_db`) encapsulada bajo el Global Query Filter multi-tenant de `CursosDbContext`. Las entidades de dominio son proyectadas a una colección inmutable `IReadOnlyList<MatriculacionDto>`, retornando `200 OK` (con arreglo vacío `[]` si no hay inscriptos) hacia Sala en Vivo para autorizar o denegar el acceso del alumno al examen.

---

## 2. ESPECIFICACIÓN DEL ENDPOINT (REST Nivel 2)

* **Ruta:** `GET /api/v1/cohortes/{cohorteId:guid}/matriculas`
* **Autenticación:** Requerida (`Authorization: Bearer <JWT>`).
* **Multi-Tenancy:** Aislamiento estricto por `InstitucionId` a nivel DbContext.
* **Query Parameters:**
  * `soloActivas` (opcional, `bool`, default: `true`): Si es `true`, filtra únicamente matrículas con `Estado == "Activa"`. Si es `false`, devuelve el historial completo (incluyendo `Baja` o `Completada`).

---

## 3. MATRIZ DE RESPUESTAS HTTP

| Status Code | Condición de Disparo | Payload / Formato |
| :--- | :--- | :--- |
| **`200 OK`** | Cohorte válida con alumnos matriculados | `[ { "id": "uuid", "institucionId": "uuid", "cohorteId": "uuid", "alumnoId": "uuid", "fechaAlta": "ISO-8601", "estado": "Activa" } ]` |
| **`200 OK`** | Cohorte válida sin alumnos matriculados | `[]` (Array JSON vacío) |
| **`400 Bad Request`** | `cohorteId` inválido (`Guid.Empty`) | RFC 9457 `ProblemDetails` |
| **`404 Not Found`** | Cohorte inexistente, ajena al tenant o inactiva (`Activo == false`) | RFC 9457 `ProblemDetails` *(Anti-enumeración)* |

---

## 4. ARQUITECTURA INTERNA (CQRS & CLEAN ARCHITECTURE)

* **Application Layer:**
  * Query: `GetMatriculacionesByCohorteQuery(Guid CohorteId, bool? SoloActivas = true)`
  * Handler: `GetMatriculacionesByCohorteQueryHandler`
  * DTO: `MatriculacionDto` (inmutable)
  * Abstracción: `IMatriculacionRepository.GetByCohorteIdAsync(Guid cohorteId, bool soloActivas, CancellationToken ct)`
* **Infrastructure Layer:**
  * Implementación EF Core: `MatriculacionRepository` sobre `CursosDbContext` con `AsNoTracking()`.
* **Testing:**
  * `GetMatriculacionesByCohorteQueryHandlerTests.cs` (6 escenarios con dobles en memoria en `Fakes.cs`). Suite total: 89 tests en verde.
