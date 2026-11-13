# Contexto Técnico de Implementación: PEI.Cursos (Historial y Matrículas)

> **Destinatario:** Modelo de lenguaje / Agente de IA en nuevas sesiones de desarrollo.  
> **Ámbito:** Microservicio `PEI.Cursos` (.NET 8 / Clean Architecture / CQRS / API First).  
> **Rama Git:** `feature/cursos-baja-matricula-historial`.  
> **Propósito:** Documento de contexto rápido y ligero con el detalle técnico de lo implementado.

---

## 1. Misión y Requerimientos Resueltos
En respuesta a los requerimientos de Frontend (gestión de cursadas inactivas y nómina de alumnos):
1. **Consulta Histórica de Cohortes:** Permitir que `GET /api/v1/cohortes/{cohorteId}/matriculas` liste las matrículas de cohortes dadas de baja (`Activo == false`).
2. **Consulta Histórica de Materiales:** Permitir que `GET /api/v1/cursos/{cursoId}/materiales` permita la lectura de apuntes en cursos dados de baja (`Activo == false`), manteniendo bloqueada la subida y eliminación.
3. **Baja Lógica de Matrícula:** Implementar `DELETE /api/v1/cohortes/{cohorteId}/matriculas/{id} -> 204 No Content`.
4. **Reactivación de Matrícula:** Implementar `POST /api/v1/cohortes/{cohorteId}/matriculas/{id}/reactivar -> 204 No Content`.

---

## 2. Decisiones Arquitectónicas Clave (Clean Architecture & REST Nivel 2)
* **Contract-First (OpenAPI):** Actualizado `contracts/openapi/cursos-v1.json` con los nuevos endpoints y respuestas 204, 401, 403, 404 y 409 Conflict.
* **Manejo de Conflictos de Estado (409 Conflict):**
  - Las transiciones inválidas en `Matriculacion.cs` (`DarDeBaja()` cuando ya está en baja o completada; `Reactivar()` cuando ya está activa o completada) lanzan `InvalidOperationException`.
  - El middleware transversal `ProblemDetailsMiddleware` captura `InvalidOperationException` y lo traduce automáticamente a `409 Conflict` (RFC 9457).
* **Segregación de Interfaces y Responsabilidad Única (SOLID):**
  - `IAutorizacionMaterialService` incorpora `ValidarPermisoLecturaAsync` (para lectura histórica en cursos inactivos).
  - `ValidarPermisoGestionAsync` se mantiene estricto para mutaciones (`POST` subir y `DELETE` eliminar material, exigiendo curso activo).
* **Autorización:** Ambos endpoints de matrícula usan `[Authorize(Policy = CursosAuthorization.AdministradorODocente)]`.

---

## 3. Mapa de Archivos Modificados y Creados

### 3.1. Contratos OpenAPI
* `contracts/openapi/cursos-v1.json`:
  - Agregado verbo `delete` en `/api/v1/cohortes/{cohorteId}/matriculas/{id}`.
  - Agregado endpoint `post` `/api/v1/cohortes/{cohorteId}/matriculas/{id}/reactivar`.
  - Documentado soporte de consulta histórica en `GetMatriculaciones` y `ListarMaterialesPorCurso`.

### 3.2. Capa Domain (`PEI.Cursos.Domain`)
* `Entities/Matriculacion.cs`:
  - `DarDeBaja()`: lanza `InvalidOperationException` si `Estado == Baja` o `Completada`.
  - `Reactivar()`: lanza `InvalidOperationException` si `Estado == Activa` o `Completada`.

### 3.3. Capa Application (`PEI.Cursos.Application`)
* `Interfaces/IAutorizacionMaterialService.cs`:
  - Firma `Task ValidarPermisoLecturaAsync(Guid cursoId, CancellationToken cancellationToken = default);`.
* `Services/AutorizacionMaterialService.cs`:
  - Implementación de `ValidarPermisoLecturaAsync` (permite curso inactivo; valida pertenencia al tenant y autorización de Docente/Admin).
* `UseCases/Matriculaciones/Queries/GetMatriculacionesByCohorteQueryHandler.cs`:
  - Removido `!cohorte.Activo`. Retorna `null` (404) únicamente si la cohorte no existe en la institución autenticada.
* `UseCases/Materiales/Queries/ListarMaterialesPorCursoQueryHandler.cs`:
  - Consume `ValidarPermisoLecturaAsync`.
* `UseCases/Matriculaciones/Commands/DarDeBajaMatriculacionCommand.cs` *(Nuevo)*:
  - `public record DarDeBajaMatriculacionCommand(Guid CohorteId, Guid Id);`.
* `UseCases/Matriculaciones/Commands/DarDeBajaMatriculacionCommandHandler.cs` *(Nuevo)*:
  - Carga matrícula, valida cohorteId/tenant (retorna `false` para 404 si no coincide), ejecuta `DarDeBaja()` y persiste con `await _unitOfWork.CommitAsync(cancellationToken);`.
* `UseCases/Matriculaciones/Commands/ReactivarMatriculacionCommand.cs` *(Nuevo)*:
  - `public record ReactivarMatriculacionCommand(Guid CohorteId, Guid Id);`.
* `UseCases/Matriculaciones/Commands/ReactivarMatriculacionCommandHandler.cs` *(Nuevo)*:
  - Carga matrícula, valida cohorteId/tenant, ejecuta `Reactivar()` y persiste con `await _unitOfWork.CommitAsync(cancellationToken);`.
* `DependencyInjection.cs`:
  - Registrados los handlers de `DarDeBajaMatriculacionCommand` y `ReactivarMatriculacionCommand`.

### 3.4. Capa API (`PEI.Cursos.Api`)
* `Controllers/MatriculacionesController.cs`:
  - Exposición de `[HttpDelete("{id:guid}")]` -> `DarDeBaja`.
  - Exposición de `[HttpPost("{id:guid}/reactivar")]` -> `Reactivar`.
  - Respuestas: `204 NoContent`, `404 NotFound`, `409 Conflict`. Política `AdministradorODocente`.

### 3.5. Pruebas Unitarias (`PEI.Cursos.UnitTests`)
* `Domain/Entities/MatriculacionTests.cs`:
  - Verifica `InvalidOperationException` en transiciones inválidas de `DarDeBaja` y `Reactivar`.
* `Application/Matriculaciones/GetMatriculacionesByCohorteQueryHandlerTests.cs`:
  - Verifica consulta histórica sobre cohorte inactiva.
* `Application/Matriculaciones/DarDeBajaMatriculacionCommandHandlerTests.cs` *(Nuevo)*:
  - Casos: éxito con commit atómico, cohorte no coincidente / inexistente (false sin commit), y excepciones por estado.
* `Application/Matriculaciones/ReactivarMatriculacionCommandHandlerTests.cs` *(Nuevo)*:
  - Casos: reincorporación con commit atómico, cohorte ajena (false), y excepciones por estado.
* `Application/Materiales/AutorizacionMaterialServiceTests.cs`:
  - Pruebas para `ValidarPermisoLecturaAsync` (lectura de apuntes en cursos inactivos).
* `Api/CursosAuthorizationHttpTests.cs`:
  - Documentación Swagger actualizada a 15 operaciones.
  - IDs de prueba (`Cohorte`, `Matricula`, etc.) fijados con GUIDs deterministas para compatibilidad con la fase de Discovery del runner de pruebas de Visual Studio.

---

## 4. Convenciones de Código Vigentes
* Comentarios de una sola línea: `// comentario corto`.
* Comentarios multilínea: `/* ... */` sin asteriscos repetidos en líneas intermedias.
* Sin comentarios XML `/// <summary>`.
* Persistencia: método `CommitAsync` de `IUnitOfWork`.
