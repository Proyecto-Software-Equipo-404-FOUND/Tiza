## Descripción del Cambio
<!-- Explicá brevemente qué problema resuelve o qué funcionalidad agrega esta PR -->
**ID de Jira / Tarea:** 

---

### Checklist de "Tarea Terminada" (Criterios de Aprobación de la Cátedra)
Marcá con una `x` los puntos que apliquen a tu tarea:

#### Arquitectura y Código (.NET)
- [ ] La lógica de negocio está en `Domain` o `Application` y **no** en el controller.
- [ ] Todo lo externo (otros servicios, IA, Redis, BD) está detrás de una **interfaz** definida en `Application`.
- [ ] Cada clase tiene una sola responsabilidad, nombres descriptivos y funciones reducidas.
- [ ] Las dependencias apuntan hacia adentro (Clean Architecture).
- [ ] Inyección de dependencias con ciclos de vida correctos: `Scoped` (DbContext, repos, handlers), `Transient` (validadores), `Singleton` (Redis, HttpClients).

#### API REST y Trazabilidad
- [ ] Las rutas usan **sustantivos en plural y sin verbos** (ej: `/api/v1/cursos/{id}/cohortes`).
- [ ] Códigos de estado HTTP adecuados: `200`, `201` (con header `Location`), `204`, `400`, `401`, `403`, `404`, `409`.
- [ ] Errores estructurados en formato `ProblemDetails` (RFC 9457).
- [ ] Logs estructurados con propagación del header `X-Correlation-ID`.
- [ ] OpenAPI / Swagger autogenerado y actualizado.

#### Base de Datos & Concurrencia
- [ ] Toda nueva tabla o consulta incluye `InstitucionId` (aislamiento multi-tenant).
- [ ] El esquema cambió **únicamente mediante una migración** de EF Core (sin SQL manual).
- [ ] Operaciones de múltiples escrituras usan transacciones ACID con rollback ante fallo.
- [ ] Control de concurrencia optimista implementado con campo `Version` donde aplique (respondiendo `409 Conflict`).

#### Frontend & UX (si aplica)
- [ ] Muestra estados de carga (spinners / skeleton loaders).
- [ ] Muestra mensajes claros de éxito y error (incluyendo feedback específico para `409`).
- [ ] Botones deshabilitados durante peticiones para evitar envíos duplicados.
- [ ] Uso de la paleta y componentes oficiales de Tiza.

#### Calidad & Pruebas
- [ ] Pruebas unitarias agregadas/actualizadas y pasando en verde (`dotnet test`).
- [ ] Request de Postman exportada o actualizada en la colección compartida.
- [ ] No se comitearon credenciales, secretos ni archivos `.env`.
