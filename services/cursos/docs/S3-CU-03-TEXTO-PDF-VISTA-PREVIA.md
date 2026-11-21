# CONTRATO E INTEGRACIÓN: S3-CU-03 — TEXTO DEL PDF (VISTA PREVIA E IA)
> **Microservicio:** PEI.Cursos  
> **Issue Asociado:** #229 (S3-CU-03)  
> **Rama de Trabajo:** `feature/S3-CU-03-texto-pdf-vista-previa`  
> **Target Consumers:** Frontend Web Docente (S3-FE-05 / #236) y Microservicio IA (S3-IA-02 / #241)  
> **Fecha:** Noviembre 2026  
> **Estado:** Implementado & Verificado con TDD (164/164 Unit Tests PASS)

---

## 1. FLUJO DE EJECUCIÓN END-TO-END (AI-to-AI)

La petición entrante `GET /api/v1/cursos/{cursoId}/materiales/{id}/texto` es interceptada por `TenantContextMiddleware`, que resuelve `InstitucionId`, `UsuarioId` y `Rol` desde los claims del JWT. `MaterialesController` delega la ejecución mediante `IMediator.SendAsync` a `ObtenerTextoMaterialPublicoQueryHandler`.

El handler invoca `IAutorizacionMaterialService.ValidarPermisoLecturaAsync(cursoId)` para validar multi-tenancy y permisos: admite lectura histórica en cursos inactivos tanto para `Administrador` como para el `Docente` asignado a alguna cohorte del curso; rechaza con `403 Forbidden` al rol `Alumno` (RN-MAT-02) o docentes no asignados, y emite `404 Not Found` ante cursos inexistentes o de otra institución.

Posteriormente, consulta `IMaterialCursoRepository.GetByIdAsync(id)`. Se aplica la regla anti-enumeración y consistencia jerárquica: si el material no existe, está inactivo (`Activo == false`) o su `CursoId` no coincide con el de la ruta, se emite `404 Not Found`. Si el apunte no ha completado su procesamiento (`Estado != EstadoMaterialCurso.Listo`), se lanza `InvalidOperationException` mapeada automáticamente por `ProblemDetailsMiddleware` a **`409 Conflict`** (`MATERIAL_NO_LISTO` en OpenAPI).

Finalmente, se evalúa `tieneTexto = !string.IsNullOrWhiteSpace(material.TextoExtraido)`. Si es `false` (PDF escaneado sin caracteres seleccionables), se retorna `texto = string.Empty` y `tieneTexto = false` sin interrumpir la operación, permitiendo al frontend advertir al usuario sin fallar. Se proyecta y retorna `TextoMaterialDto` con código **`200 OK`**.

El endpoint interno previo `GET /api/v1/materiales/{id}/texto` se mantiene intacto para el consumo directo de IA / MCP.

---

## 2. ESPECIFICACIÓN DEL ENDPOINT (RESTful Nivel 2)

* **Ruta:** `GET /api/v1/cursos/{cursoId:guid}/materiales/{id:guid}/texto`
* **Autenticación:** Requerida (`Authorization: Bearer <JWT>`).
* **Roles Permitidos:** `Administrador`, `Docente` (con cohorte en el curso). Alumno $\rightarrow$ 403.
* **Respuesta Exitosa (`200 OK`):**
  ```json
  {
    "materialId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "titulo": "Unidad 1 - Conceptos Generales",
    "cantidadPaginas": 15,
    "texto": "Contenido extraido del documento...",
    "tieneTexto": true
  }
  ```

---

## 3. MATRIZ DE RESPUESTAS HTTP

| Status Code | Causa / Condición de Disparo | Formato de Salida |
| :--- | :--- | :--- |
| **`200 OK`** | Consulta exitosa del texto (incluye bandera `tieneTexto: false` si es escaneado) | `TextoMaterialDto` |
| **`401 Unauthorized`** | Token JWT ausente, expirado o con firma inválida | RFC 9457 `ProblemDetails` |
| **`403 Forbidden`** | Rol Alumno (RN-MAT-02) o Docente sin cohorte asignada en el curso | RFC 9457 `ProblemDetails` |
| **`404 Not Found`** | Curso o Material inexistente, dado de baja o ajeno al curso / institución | RFC 9457 `ProblemDetails` *(Anti-enumeración)* |
| **`409 Conflict`** | El apunte aún está en estado `Procesando` o finalizó en `Error` (`MATERIAL_NO_LISTO`) | RFC 9457 `ProblemDetails` |

---

## 4. MAPEO ARQUITECTÓNICO (CLEAN ARCHITECTURE & CQRS)

* **Application (`PEI.Cursos.Application`):**
  * DTO: `TextoMaterialDto(Guid MaterialId, string Titulo, int CantidadPaginas, string Texto, bool TieneTexto)`
  * Query: `ObtenerTextoMaterialPublicoQuery(Guid CursoId, Guid MaterialId)`
  * Handler: `ObtenerTextoMaterialPublicoQueryHandler`
  * Autorización: `IAutorizacionMaterialService.ValidarPermisoLecturaAsync(Guid cursoId, CancellationToken ct)`
* **Api (`PEI.Cursos.Api`):**
  * `MaterialesController`: Mapeo de ruta `cursos/{cursoId:guid}/materiales/{id:guid}/texto` con decoradores OpenAPI acordados en PR #253.
* **Testing (`PEI.Cursos.UnitTests`):**
  * `ObtenerTextoMaterialPublicoQueryHandlerTests.cs`: 10 pruebas unitarias con dobles en memoria (`Fakes.cs`) cubriendo lectura docente/admin, PDF sin texto, bloqueos 403, anti-enumeración 404 y estado transicional 409. Suite total: 164 pruebas en verde.
