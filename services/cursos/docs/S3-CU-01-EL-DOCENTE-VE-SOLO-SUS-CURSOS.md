# DOCUMENTACIÓN DE CASO DE USO: S3-CU-01
> **Microservicio:** PEI.Cursos  
> **Historia de Usuario:** Como docente, quiero ver solo los cursos que dicto, para no confundirme con los de otros docentes.  
> **Issue:** [#227](https://github.com/Proyecto-Software-Equipo-404-FOUND/Tiza/issues/227)  
> **Rama:** `feature/S3-CU-01-docente-solo-ve-sus-cursos`  
> **Revisa el PR:** Augusto (`@auguseba200208-prog`)  
> **Estado:** Implementado & Verificado (165 Unit Tests PASS - 0 Regresiones)

---

## 1. FLUJO DE EJECUCIÓN END-TO-END

### A. Listado de Cursos (`GET /api/v1/cursos`)
1. **Intercepción y Contexto:**
   `TenantContextMiddleware` valida el JWT emitido por `PEI.Identidad`, resolviendo `InstitucionId`, `UsuarioId` y `Rol` (`Docente` o `Administrador`).
2. **Despacho del Query:**
   `CursosController.GetCursos` envía `GetCursosQuery` a través de `IMediator.SendAsync`.
3. **Lógica de Aplicación (CQRS):**
   `GetCursosQueryHandler` evalúa el rol: si es `Docente`, delega `UsuarioId` como `docenteId` al repositorio; si es `Administrador`, pasa `null` para ver todo el catálogo institucional.
4. **Capa de Persistencia (PostgreSQL):**
   `CursoRepository.GetPaginadosAsync` ejecuta una consulta `AsNoTracking()` con filtro multi-tenant y subconsulta `WHERE EXISTS (SELECT 1 FROM "Cohortes" ... WHERE DocenteId = @docenteId)`.
5. **Retorno:**
   Mapeo inmutable a `ResultadoPaginadoDTO<CursoDto>` retornando `200 OK`.

### B. Detalle de Curso (`GET /api/v1/cursos/{id}`)
1. **Validación de Titularidad:**
   `GetCursoByIdQueryHandler` verifica mediante `ICohorteRepository.ExisteDocenteEnCursoAsync` si el docente dicta comisiones en el curso.
2. **Defensa Anti-Enumeración:**
   Si el curso no existe o si pertenece a otro docente, el handler retorna `null` y el Controller responde `404 Not Found` (en vez de `403`), ocultando la existencia del recurso.

### C. Edición y Baja de Cursos (`PUT /api/v1/cursos/{id}` y `DELETE /api/v1/cursos/{id}`)
1. **Regla de Negocio RN-CUR-01:**
   La mutación estructural y de metadatos de los cursos es potestad exclusiva del **Administrador**. Los docentes no editan cursos (ni siquiera los propios).
2. **Control de Autorización:**
   `PUT /api/v1/cursos/{id}` y `DELETE /api/v1/cursos/{id}` exigen la política `[Authorize(Policy = CursosAuthorization.SoloAdministrador)]`.
3. **Respuesta ante Docentes:**
   Un usuario con rol `Docente` recibe inmediatamente `403 Forbidden`.

---

## 2. DECISIONES DE DISEÑO (ALINEACIÓN TÉCNICA / PO)

* **P1: ¿Qué comisiones determinan la visibilidad de los cursos para el docente?**
  * **Decisión:** Cualquier comisión asignada, sea activa o histórica.
  * **Por qué:** Cumple con la regla de lectura histórica (RN-COH-01). El docente debe poder consultar cursos de ciclos lectivos pasados.
* **P2: ¿Qué responder si un docente intenta ver (GET) un curso ajeno?**
  * **Decisión:** `404 Not Found` (no `403`).
  * **Por qué:** Regla anti-enumeración (seguridad OWASP multi-tenant). Un `403` confirmaría que el curso existe; el `404` lo trata como inexistente.
* **P3: ¿El docente puede editar sus cursos (`PUT /api/v1/cursos/{id}`)?**
  * **Decisión:** No. Conforme a la aclaración del Product Owner y la regla `RN-CUR-01`, la creación, modificación y baja de cursos es exclusiva del `Administrador`.
  * **Por qué:** Mantiene la gobernanza institucional sobre los planes de estudio y catálogo de cursos; el docente gestiona sus cohortes y evaluaciones, pero no los metadatos institucionales del curso. Si un docente intenta hacer `PUT`, el endpoint responde `403 Forbidden`.
* **P4: ¿Cómo y dónde se aplica el filtro de cursos por docente?**
  * **Decisión:** En PostgreSQL mediante `c.Cohortes.Any(co => co.DocenteId == id)` en `CursoRepository`.
  * **Por qué:** Rendimiento y paginación en la base de datos sin sobrecargar memoria en la API (subconsulta SQL `EXISTS`).
* **P5: ¿Dónde se resuelve el rol y el ID del docente autenticado?**
  * **Decisión:** En la capa `Application` consumiendo `ITenantContext`.
  * **Por qué:** Clean Architecture. Mantiene controladores delgados (*thin controllers*) y encapsula la lógica contextual en los handlers.

---

## 3. ESPECIFICACIÓN DE ENDPOINTS (REST NIVEL 2)

### 1. `GET /api/v1/cursos`
* **Políticas:** `[Authorize(Policy = CursosAuthorization.AdministradorODocente)]`
* **Query Params:** `pagina` (default 1), `tamanoPagina` (default 10), `area` (opcional), `busqueda` (opcional).
* **Visibilidad:** Docente recibe solo sus cursos; Administrador recibe catálogo completo.

### 2. `GET /api/v1/cursos/{id:guid}`
* **Políticas:** `[Authorize(Policy = CursosAuthorization.AdministradorODocente)]`
* **Respuestas:** `200 OK` (propio / admin) o `404 Not Found` (ajeno / inexistente).

### 3. `PUT /api/v1/cursos/{id:guid}`
* **Políticas:** `[Authorize(Policy = CursosAuthorization.SoloAdministrador)]`
* **Body:** `ActualizarCursoDto { Nombre, Area }`
* **Respuestas:** `200 OK` (actualizado por admin), `400 Bad Request` (validación), `403 Forbidden` (docente / alumno), `404 Not Found` (inexistente), `409 Conflict` (nombre duplicado en institución).

---

## 4. MATRIZ DE RESPUESTAS HTTP

| Endpoint | Verbo | Rol | Condición | Código HTTP |
| :--- | :--- | :--- | :--- | :--- |
| `/api/v1/cursos` | `GET` | Anónimo | Sin token JWT o inválido | `401 Unauthorized` |
| `/api/v1/cursos` | `GET` | Alumno | Rol no contemplado en política | `403 Forbidden` |
| `/api/v1/cursos` | `GET` | Docente | Cursos propios paginados | `200 OK` (Filtrado) |
| `/api/v1/cursos` | `GET` | Administrador | Catálogo general institucional | `200 OK` (Total) |
| `/api/v1/cursos/{id}` | `GET` | Docente | Curso donde dicta comisiones | `200 OK` |
| `/api/v1/cursos/{id}` | `GET` | Docente | Curso donde **no** dicta comisiones | `404 Not Found` *(Anti-enumeración)* |
| `/api/v1/cursos/{id}` | `GET` | Administrador | Curso existente en la institución | `200 OK` |
| `/api/v1/cursos/{id}` | `PUT` | Docente | Intento de modificación | `403 Forbidden` *(RN-CUR-01)* |
| `/api/v1/cursos/{id}` | `PUT` | Administrador | Curso actualizado correctamente | `200 OK` |
| `/api/v1/cursos/{id}` | `PUT` | Administrador | Nombre duplicado en el tenant | `409 Conflict` |
| `/api/v1/cursos/{id}` | `DELETE` | Docente | Acción restringida a administradores | `403 Forbidden` |

---

## 5. ARQUITECTURA Y COMPONENTES (CQRS & CLEAN CODE)

* **Dominio (`PEI.Cursos.Domain`):**
  * Entidad `Curso` con navegación hacia `ICollection<Cohorte> Cohortes`.
  * Entidad `Cohorte` con `CursoId` y `DocenteId`.
* **Aplicación (`PEI.Cursos.Application`):**
  * Contratos: `ICursoRepository.GetPaginadosAsync` (soporta `docenteId?`) y `ICohorteRepository.ExisteDocenteEnCursoAsync`.
  * Handlers: `GetCursosQueryHandler`, `GetCursoByIdQueryHandler`, `ActualizarCursoCommandHandler`.
* **Infraestructura (`PEI.Cursos.Infrastructure`):**
  * `CursoRepository` y `CohorteRepository` con consultas EF Core `AsNoTracking()` optimizadas para PostgreSQL.
* **Testing (`PEI.Cursos.UnitTests`):**
  * Suite completa con 165 pruebas unitarias en verde cubriendo roles, paginación, autorización HTTP y Swagger.

---

## 6. GUÍA DE DEFENSA ANTE LA CÁTEDRA

Puntos clave para justificar el código ante los docentes:

1. **¿Por qué retornar 404 Not Found en lugar de 403 Forbidden en un curso ajeno en GET?**
   * *Justificación:* Principio de **Anti-Enumeración**. Un `403` revela que el recurso con ese UUID existe en el sistema. El `404` trata el recurso como inexistente para el usuario, impidiendo el escaneo de IDs por fuerza bruta.
2. **¿Por qué la consulta de cursos no trae datos a memoria antes de filtrar?**
   * *Justificación:* En `CursoRepository`, el predicado `c.Cohortes.Any(...)` se construye sobre `IQueryable`. Entity Framework Core lo traduce a un operador SQL `EXISTS` ejecutado directamente en PostgreSQL, asegurando paginación y conteo en base de datos sin sobrecargar la RAM del servicio.
3. **¿Cómo se aplican SOLID y Clean Architecture en este caso de uso?**
   * *Justificación:*
     * **SRP:** El controlador solo expone la ruta HTTP y su política de autorización; los handlers orquestan las reglas de negocio; los repositorios encapsulan la persistencia.
     * **DIP:** La lógica de aplicación depende únicamente de interfaces (`ICursoRepository`, `ICohorteRepository`, `ITenantContext`). Esto permitió implementar la suite completa con TDD y repositorios `Fake` antes de conectar la infraestructura.
