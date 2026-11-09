# ESPECIFICACIÓN TÉCNICA E INTEGRACIÓN: S2-CU-01 (FASE 2) — MATERIAL PDF
> **Microservicio:** PEI.Cursos  
> **Issue Asociado:** #137 (S2-CU-01)  
> **Rama de Trabajo:** `feature/S2-CU-01-fase2-material-pdf`  
> **Target Consumers:** Frontend Docente (Web) / Microservicio IA (Recurso MCP `apunte://{materialId}`)  
> **Fecha:** 2026-11-06  
> **Estado:** Implementado & Verificado con Pruebas Unitarias

---

## 1. CONTEXTO OPERATIVO Y DECISIONES TÉCNICAS (AI-to-AI)

Este documento condensa los acuerdos técnicos y arquitectónicos formalizados para la implementación de la subida, procesamiento y consulta de apuntes PDF:

1. **Ciclo de Vida del POST en Memoria:** La validación de límites (máx 10 MB, máx 30 páginas, formato PDF) y la extracción de texto con **PdfPig** se ejecutan en memoria directamente sobre el `Stream`. Solo si el archivo supera todas las reglas, se persiste en disco mediante `IAlmacenamientoArchivos` y se registra en PostgreSQL con `Estado = Listo`. Si falla cualquier regla, se rechaza inmediatamente (400/415) sin dejar archivos huérfanos en disco ni filas inconsistentes en la base de datos.
2. **Almacenamiento Desacoplado (DIP/OCP):** El binario PDF **nunca se persiste en PostgreSQL**. Se almacena físicamente mediante `IAlmacenamientoArchivos` (disco local configurable por `appsettings.json` en desarrollo; Supabase Storage/R2 en producción) bajo la clave relativa organizada `{institucionId}/{cursoId}/{materialId}.pdf`. En la base de datos solo se guardan los metadatos y el texto extraído.
3. **Extracción y Validación de Texto (RN-MAT-03 / RN-MAT-04):** `IExtractorTextoPdf` valida un máximo de 30 páginas y extrae texto sin OCR. Si el PDF es escaneado o el texto extraído está vacío/en blanco, emite `DomainException` con el mensaje: *"El PDF no tiene texto seleccionable. Subí un PDF generado desde un procesador de texto, no escaneado."* Las páginas se unen con salto de línea doble (`\n\n`).
4. **Autorización y Anti-Enumeración (RN-MAT-01 / RN-MAT-02):** Centralizada en `IAutorizacionMaterialService` (Application). Si el curso no existe o pertenece a otra institución $\rightarrow$ `KeyNotFoundException` (`404 Not Found` anti-enumeración). Si el rol es `Alumno` o es un `Docente` sin cohorte activa en dicho curso $\rightarrow$ `ForbiddenException` (`403 Forbidden`).
5. **Manejo de Errores RFC 9457:** Incorporación de `ForbiddenException` (403) y `UnsupportedMediaTypeException` (415) en `PEI.BuildingBlocks`, capturadas por `ProblemDetailsMiddleware`.
6. **Baja Lógica y Preservación Histórica (RN-MAT-05):** El borrado (`DELETE`) ejecuta una baja lógica (`Activo = false`) conservando intacto el archivo físico en disco para garantizar la trazabilidad de evaluaciones previamente generadas.

---

## 2. ESPECIFICACIÓN DE ENDPOINTS (RESTful Nivel 2 / OpenAPI `cursos-v1.json`)

* **`POST /api/v1/cursos/{cursoId}/materiales`**
  * **Payload:** `multipart/form-data` con campo `archivo` (binario PDF) y opcional `titulo` (string, máx 150).
  * **Respuesta Exitosa:** `201 Created` con header `Location: /api/v1/cursos/{cursoId}/materiales/{id}` y cuerpo `MaterialCursoDto`.
* **`GET /api/v1/cursos/{cursoId}/materiales`**
  * **Descripción:** Lista los apuntes activos del curso ordenados del más reciente al más antiguo, omitiendo el campo de texto.
  * **Respuesta Exitosa:** `200 OK` con `IReadOnlyList<MaterialCursoDto>`.
* **`DELETE /api/v1/cursos/{cursoId}/materiales/{id}`**
  * **Descripción:** Ejecuta la baja lógica del material.
  * **Respuesta Exitosa:** `204 No Content`.
* **`GET /api/v1/materiales/{id}/texto`**
  * **Descripción:** Endpoint interno consumido por el microservicio de IA (recurso MCP). Exige autenticación, rechaza Alumnos con 403 y requiere que el material exista, esté activo y en estado `Listo` (404 si no cumple).
  * **Respuesta Exitosa:** `200 OK` con `TextoMaterialDto`.

---

## 3. MATRIZ DE RESPUESTAS HTTP

| Status Code | Causa / Condición | Formato de Salida |
| :--- | :--- | :--- |
| **`200 OK`** | Listado de materiales o consulta de texto interno exitosa | `MaterialCursoDto[]` / `TextoMaterialDto` |
| **`201 Created`** | Apunte subido, texto extraído y persistido exitosamente | `MaterialCursoDto` + Header `Location` |
| **`204 No Content`** | Baja lógica exitosa del apunte | Vacío |
| **`400 Bad Request`** | Supera 10 MB, supera 30 páginas o PDF sin texto seleccionable (RN-MAT-03 / RN-MAT-04) | RFC 9457 `ProblemDetails` |
| **`401 Unauthorized`** | Token JWT ausente, expirado o con firma inválida | RFC 9457 `ProblemDetails` |
| **`403 Forbidden`** | Rol Alumno o Docente no asignado a cohorte activa del curso (RN-MAT-01 / RN-MAT-02) | RFC 9457 `ProblemDetails` |
| **`404 Not Found`** | Curso o Material inexistente, inactivo o perteneciente a otra institución | RFC 9457 `ProblemDetails` *(Anti-enumeración)* |
| **`415 Unsupported Media Type`** | Archivo subido no posee extensión o contenido PDF | RFC 9457 `ProblemDetails` |

---

## 4. MAPEO ARQUITECTÓNICO (CLEAN ARCHITECTURE & CQRS)

* **Domain (`PEI.Cursos.Domain`):**
  * Entidad: `MaterialCurso` (hereda de `BaseEntity`, encapsula validaciones, invariantes, `Desactivar()` y `MarcarListo()`).
  * Enum: `EstadoMaterialCurso` (`Procesando`, `Listo`, `Error`).
* **Application (`PEI.Cursos.Application`):**
  * Comandos: `SubirMaterialCommand`, `EliminarMaterialCommand`.
  * Consultas: `ListarMaterialesPorCursoQuery`, `ObtenerTextoMaterialQuery`.
  * DTOs: `MaterialCursoDto`, `TextoMaterialDto`.
  * Interfaces: `IAlmacenamientoArchivos`, `IExtractorTextoPdf`, `IAutorizacionMaterialService`, `IMaterialCursoRepository`.
* **Infrastructure (`PEI.Cursos.Infrastructure`):**
  * Adaptadores: `AlmacenamientoLocalArchivos`, `PdfPigExtractorTextoPdf`.
  * Persistencia: `MaterialCursoConfiguration` (Fluent API), migración EF Core y `MaterialCursoRepository`.
* **Api (`PEI.Cursos.Api`):**
  * `MaterialesController`: Mapeo de rutas RESTful Nivel 2, soporte multipart/form-data, cabeceras `Location` y códigos HTTP conformes a RFC 9457.
* **Testing (`PEI.Cursos.UnitTests`):**
  * `MaterialCursoTests.cs`: Pruebas unitarias de invariantes de dominio (máx 10 MB, máx 30 páginas, texto seleccionable y baja lógica).
  * `AutorizacionMaterialServiceTests.cs`: Pruebas de seguridad multi-tenant y roles (Docente asignado, Alumno 403, Admin y anti-enumeración 404).
  * `SubirMaterialCommandHandlerTests.cs`: Orquestación completa de subida, extracción con PdfPig, storage desacoplado y rechazo de formato no PDF (415).
  * `EliminarMaterialCommandHandlerTests.cs`: Baja lógica sin eliminación física del archivo en disco (RN-MAT-05).
  * `ListarMaterialesPorCursoQueryHandlerTests.cs`: Consulta optimizada con AsNoTracking y orden cronológico descendente.
  * `ObtenerTextoMaterialQueryHandlerTests.cs`: Consumo interno para IA/MCP con bloqueo a alumnos (403) y validación de estado Listo (404).
  * Dobles de prueba (`Fakes.cs`): Implementaciones en memoria de `FakeMaterialCursoRepository`, `FakeAlmacenamientoArchivos` y `FakeExtractorTextoPdf`.
