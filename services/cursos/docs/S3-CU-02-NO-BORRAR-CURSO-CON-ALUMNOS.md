# CONTRATO E INTEGRACIÓN: S3-CU-02 — NO BORRAR CURSO CON ALUMNOS

> **Microservicio:** PEI.Cursos  
> **Historia de Usuario / Issue:** [#228 (S3-CU-02)](https://github.com/Proyecto-Software-Equipo-404-FOUND/Tiza/issues/228)  
> **Rama de Trabajo:** `feature/S3-CU-02-no-borrar-curso-con-alumnos`  
> **Revisor Asignado:** Augusto (`@auguseba200208-prog`)  
> **Fecha:** Noviembre 2026  
> **Estado:** Implementado & Verificado con TDD (161/161 Unit Tests PASS)  

---

## 1. FLUJO DE EJECUCIÓN END-TO-END (AI-to-AI)

1. **Ingreso y Autenticación:** La petición HTTP `DELETE /api/v1/cursos/{id}` ingresa por el API Gateway (YARP) hacia `PEI.Cursos.Api`.
2. **Aislamiento Multi-Tenant:** `TenantContextMiddleware` intercepta el token JWT, resolviendo `InstitucionId`, `UsuarioId` y `Rol`.
3. **Control de Acceso en Controller:** `CursosController.DesactivarCurso` valida la directiva `[Authorize(Policy = CursosAuthorization.SoloAdministrador)]`. Si el rol no es Administrador, responde `403 Forbidden`.
4. **Despacho CQRS:** El controlador despacha `DesactivarCursoCommand(id)` a través de `IMediator.SendAsync` hacia `DesactivarCursoCommandHandler`.
5. **Verificación de Existencia (Anti-Enumeración):** El handler consulta `_cursoRepository.GetByIdAsync(id)`. Si el curso no existe o pertenece a otra institución (filtrado por el Global Query Filter de EF Core), retorna `false` y el controller emite **`404 Not Found`** para no revelar existencia.
6. **Validación de Matrículas Activas:** El handler consulta `_matriculacionRepository.ExisteActivaEnCursoAsync(id)`.
   * **SQL Delegado en PostgreSQL:**
     ```sql
     SELECT EXISTS (
         SELECT 1 
         FROM "Matriculaciones" AS m
         WHERE m."InstitucionId" = @InstitucionActual
           AND m."CursoId" = @CursoId
           AND m."Estado" = 'Activa'
     );
     ```
   * **Rendimiento:** Ejecuta en tiempo $O(1)$ aprovechando el índice único condicional `UX_Matriculaciones_Activa_Institucion_Curso_Alumno`. No carga entidades ni colecciones en memoria RAM.
7. **Resolución de Conflicto:** Si existen alumnos activos, lanza `CursoConAlumnosException`. `ProblemDetailsMiddleware` captura la excepción (heredada de `CodedConflictException`), mapeando a HTTP **`409 Conflict`** con payload RFC 9457 y código estructurado `"type": "CURSO_CON_ALUMNOS"`.
8. **Baja Lógica y Transacción:** Si no existen alumnos activos, invoca `curso.Desactivar()`, verificando las invariantes de dominio (arroja `DomainException` $\rightarrow$ `400 Bad Request` si ya estaba inactivo). Se confirma la persistencia atómica con `_unitOfWork.CommitAsync` y se retorna HTTP **`204 No Content`**.

---

## 2. DECISIONES DE DISEÑO (SÍNTESIS DE SESIÓN GRILL-ME)

| Pregunta de Diseño | Decisión Técnica Adoptada | Justificación Arquitectónica |
| :--- | :--- | :--- |
| **P1: ¿Estrategia de ramificación y dependencia con PR #258?** | Integrar sobre los cambios mergeados de la PR [#258](https://github.com/Proyecto-Software-Equipo-404-FOUND/Tiza/pull/258) en `main`. | Permite utilizar de forma nativa la columna `CursoId` en `Matriculaciones`, el índice parcial y `CodedConflictException` sin migraciones redundantes. |
| **P2: ¿Qué estados de matrícula impiden el borrado?** | Estrictamente `Estado == EstadoMatriculacion.Activa`. | Alumnos cursando activamente no deben perder su espacio; cursos con historial finalizado (`Baja` o `Completada`) pueden darse de baja de forma segura. |
| **P3: ¿Cómo consultar los alumnos activos?** | Método `ExisteActivaEnCursoAsync` en `IMatriculacionRepository` con `AnyAsync`. | Respeta Inversión de Dependencias (DIP) y Single Responsibility (SRP); genera un `EXISTS` nativo en PostgreSQL en tiempo $O(1)$. |
| **P4: ¿Cómo modelar el error 409?** | Crear `CursoConAlumnosException : CodedConflictException("CURSO_CON_ALUMNOS", ...)`. | Estandariza errores de negocio RFC 9457 inyectando el código exacto requerido por el frontend para feedback contextual al docente/admin. |
| **P5: ¿Alcance sobre cohortes asociadas?** | Baja lógica simple del `Curso` (`curso.Desactivar()`), sin cascada. | Respeta el alcance definido en el issue (excluye archivado o borrado en cascada), preservando la integridad referencial. |
| **P6: ¿Contratos OpenAPI y Living Doc?** | Sincronizar respuesta 409 en `cursos-v1.json` y generar este living doc. | Garantiza cumplimiento estricto de «Contrato Primero» y prepara al alumno para la defensa académica ante los docentes. |

---

## 3. ESPECIFICACIÓN DEL ENDPOINT (RESTful NIVEL 2)

* **Método HTTP:** `DELETE`
* **Ruta:** `/api/v1/cursos/{id:guid}`
* **Políticas de Autorización:** `Administrador` (Claim `role = Administrador`).
* **Header de Autenticación:** `Authorization: Bearer <JWT>`
* **Cabecera de Trazabilidad:** `X-Correlation-ID: <GUID>` (opcional / autogenerado).
* **Respuesta Exitosa (`204 No Content`):** Sin cuerpo.

---

## 4. MATRIZ DE RESPUESTAS HTTP

| Código HTTP | Condición de Disparo | Formato de Salida | Rol Permitido |
| :---: | :--- | :--- | :--- |
| **`204 No Content`** | Curso dado de baja lógica exitosamente (`Activo = false`). | Vacío | Administrador |
| **`400 Bad Request`** | El curso ya se encuentra inactivo previamente (`DomainException`). | `ProblemDetails` | Administrador |
| **`401 Unauthorized`** | Token JWT ausente, expirado o con firma inválida. | `ProblemDetails` | Anónimo |
| **`403 Forbidden`** | Usuario autenticado con rol distinto de Administrador (ej. Docente o Alumno). | `ProblemDetails` | Docente, Alumno |
| **`404 Not Found`** | Curso inexistente o perteneciente a otra institución *(Anti-enumeración)*. | `ProblemDetails` | Administrador |
| **`409 Conflict`** | El curso posee comisiones con al menos una matrícula activa (`CURSO_CON_ALUMNOS`). | `ProblemDetails` con `type` | Administrador |

### Ejemplo de Payload de Error 409 (RFC 9457 ProblemDetails):
```json
{
  "type": "CURSO_CON_ALUMNOS",
  "title": "No se puede dar de baja el curso porque tiene alumnos con matrícula activa en sus comisiones. Dalos de baja primero.",
  "status": 409,
  "correlationId": "4f9e00a3-12e1-4c7e-a4f5-8556de0ecd03"
}
```

---

## 5. MAPEO ARQUITECTÓNICO (CLEAN ARCHITECTURE & CQRS)

* **Domain (`PEI.Cursos.Domain`):**
  * `Matriculacion.cs`: Entidad con `CursoId` y relación hacia `Curso`.
  * `Curso.cs`: Método de negocio `Desactivar()` que valida invariante de estado activo.
* **Application (`PEI.Cursos.Application`):**
  * `IMatriculacionRepository.cs`: Contrato con método `ExisteActivaEnCursoAsync(Guid cursoId, CancellationToken ct)`.
  * `CursoConAlumnosException.cs`: Excepción de conflicto con código `"CURSO_CON_ALUMNOS"`.
  * `DesactivarCursoCommandHandler.cs`: Orquestación CQRS validando alumnos activos antes de la baja.
* **Infrastructure (`PEI.Cursos.Infrastructure`):**
  * `MatriculacionRepository.cs`: Implementación con EF Core y consulta optimizada `AnyAsync` sobre `CursoId`.
* **Api (`PEI.Cursos.Api`):**
  * `CursosController.cs`: Decoración Swagger con `[ProducesResponseType(StatusCodes.Status409Conflict)]`.
* **Contracts (`contracts/`):**
  * `openapi/cursos-v1.json`: Especificación OpenAPI sincronizada con código `409` y schema RFC 9457.
* **Testing (`PEI.Cursos.UnitTests`):**
  * `Fakes.cs`: Implementación en memoria de `FakeMatriculacionRepository.ExisteActivaEnCursoAsync`.
  * `DesactivarCursoCommandHandlerTests.cs`: 5 escenarios unitarios cubriendo cursos con alumnos activos, alumnos solo en baja, sin alumnos, inexistentes e inactivos.

---

## 6. GUÍA DE DEFENSA ANTE LA CÁTEDRA

* **¿Por qué no se borran los registros físicamente de la base de datos?**  
  Se aplica borrado lógico (`Activo = false`) para preservar la trazabilidad institucional y la integridad histórica de evaluaciones, notas pasadas y estadísticas.
* **¿Por qué la consulta `ExisteActivaEnCursoAsync` es $O(1)$?**  
  Porque en PostgreSQL se apoya en el índice parcial `UX_Matriculaciones_Activa_Institucion_Curso_Alumno` (creado en la migración de la PR #258). Al solicitar `AnyAsync`, EF Core traduce a `SELECT EXISTS (...)`, deteniendo el escaneo en el primer registro coincidente sin recorrer la tabla completa.
* **¿Por qué se responde 404 y no 403 ante cursos ajenos?**  
  Por el principio de seguridad anti-enumeración: un atacante no debe poder deducir si un identificador existe en otra institución mediante códigos de error diferenciados.
* **¿Cómo se desacopla la excepción del protocolo HTTP?**  
  La capa `Application` lanza `CursoConAlumnosException`, que hereda de `CodedConflictException`. La capa `BuildingBlocks` (`ProblemDetailsMiddleware`) intercepta la excepción y la formatea según el estándar RFC 9457 con código `409`, manteniendo el dominio y la aplicación 100% desacoplados de ASP.NET Core.
