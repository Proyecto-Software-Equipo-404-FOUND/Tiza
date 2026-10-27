# QA-02 — Casos de prueba de Identidad

Responsable: Candela Luna. Fecha:24/10/2026. Base inspeccionada:853f3f6. Rama:`feature/QA-02-tests-identidad`.

## Matriz inicial antes de ampliar cobertura

La numeración de esta matriz corresponde a la fase2 de la consigna; la matriz final usa la numeración detallada de la fase3 (por ejemplo, QA02-06 final significa Alumno→Administrador).

| ID inicial | Prueba requerida | Existe | Funciona al iniciar | Acción |
|---|---|---|---|---|
| QA02-01 |Login inválido401|Sí: CredencialesFallidasDevuelven401Generico|PASS, suite inicial169/169|Mantener; complementar Newman y no filtración |
| QA02-02 |Protegido sin JWT401|Sí: RegistroAnonimoRechazado|PASS|Mantener; Newman GET protegido |
| QA02-03 |JWT inválido401|Sí: JWT claims/firma/expiración en AuthenticationHttpTests|PASS|Mantener; fixture efímero Newman |
| QA02-04 |Rol sin permisos403|Sí: HttpRolesInstitucionalesExigenRolCorrecto y DocenteNoPuedeRegistrar|PASS|Mantener; ampliar aserciones de errores y permisos en Newman |
| QA02-05 |Solicitud inválida400|Parcial: casos básicos de ID07, roles y tenant|PASS existentes; no ejecutados los casos nuevos todavía|Agregar matriz de17 variantes con no persistencia y secretos |
| QA02-06 |ProblemDetails|Sí parcial: Content-Type/status/errors|PASS existentes|Completar estructura, ausencia de tokens/diagnóstico y separación de esquemas |

Todos estos criterios pueden verificarse sin infraestructura externa: las fábricas HTTP existentes reemplazan persistencia por fakes y usan TestServer. La regresión PostgreSQL es adicional, con base dedicada, y no determina la disponibilidad de la suite principal.

## Reglas verificadas en código antes de fijar expectativas

- Login: email requerido, formato email, longitud máxima150; password requerido y máximo72 bytes UTF8. Credenciales fallidas401 genérico, sin tokens.
- Register: email como login; password mínimo8 caracteres y máximo72 bytes; nombre/apellido requeridos≤100; rol sólo Docente/Alumno, sensible a mayúsculas; institución inferida del token, JSON extra rechazado. Requiere Administrador.
- Refresh/Logout: refreshToken máximo256; refresh desconocido401; logout vacío idempotente204. No se supone Required donde no existe.
- JWT: firma, emisor, audiencia, vigencia, claims canónicos y usuario/tenant activo. Expiración y firma incorrecta401. Rol ausente401; Alumno/Docente válidos sin permiso403.
- Plataforma usa otro esquema, issuer, audience y clave. Token institucional401 `application/json`. Los errores institucionales usan `application/problem+json`. Para400 de binding/DTO se exige `errors`; `type/detail/instance` sólo se validan si existen.

Fuentes: `RegisterCommand`, `LoginCommand`, `PasswordBytesAttribute`, `RefreshCommand`, `LogoutCommand`, `IdentityExceptionHandler`, `AddIdentityAuthentication`, `PlataformaSetup`, `IdentityOpenApiTests` e `ID07-SWAGGER-POSTMAN.md`. No se agregaron reglas de negocio.

## Fixture y datos

Tres usuarios sintéticos en memoria; las cuentas, claves y passwords no se reutilizan de desarrollo. Los tokens válidos y negativos provienen del fixture de testing y no se guardan. `{{qa02Password}}` es efímero; el password largo tiene37 caracteres multibyte (74 bytes), para probar la restricción real UTF8. Los JSON incompletos, roles no permitidos y cadenas largas no contienen datos reales.

Cada caso es independiente: no utiliza login o altas ejecutados manualmente antes. Los controles positivos C1–C3 acreditan que los tokens por rol son aceptados por su endpoint correspondiente. El estado inicial/final del store debe mantenerse en tres usuarios y cero altas globales. Todas las peticiones requieren el fixture autorizado; POST además requiere allowMutations=true.

## Matriz final de casos

| ID | Objetivo | Endpoint | Método | Precondición | Datos sintéticos | Esperado | Obtenido | Estado |
|---|---|---|---|---|---|---|---|---|
| QA02-01 | Contraseña incorrecta | /api/auth/login | POST | Fixture en memoria; anónimo; allowMutations=true | {"email":"{{qa02AdminEmail}}","password":"{{qa02WrongPassword}}"} | 401; status/title, sin secretos | 401; application/problem+json; todas las aserciones aprobadas | PASS |
| QA02-01b | Cuenta inexistente | /api/auth/login | POST | Fixture en memoria; anónimo; allowMutations=true | {"email":"unknown@test.invalid","password":"{{qa02WrongPassword}}"} | 401; status/title, sin secretos | 401; application/problem+json; todas las aserciones aprobadas | PASS |
| QA02-02 | Sin JWT | /api/auth/administrador | GET | Fixture en memoria; anónimo | Sin cuerpo; token del perfil indicado | 401; status/title, sin secretos | 401; application/problem+json; todas las aserciones aprobadas | PASS |
| QA02-03 | JWT malformado | /api/auth/administrador | GET | Fixture en memoria; qa02MalformedToken | Sin cuerpo; token del perfil indicado | 401; status/title, sin secretos | 401; application/problem+json; todas las aserciones aprobadas | PASS |
| QA02-04 | JWT expirado | /api/auth/administrador | GET | Fixture en memoria; qa02ExpiredToken | Sin cuerpo; token del perfil indicado | 401; status/title, sin secretos | 401; application/problem+json; todas las aserciones aprobadas | PASS |
| QA02-05 | Firma incorrecta | /api/auth/administrador | GET | Fixture en memoria; qa02WrongSignatureToken | Sin cuerpo; token del perfil indicado | 401; status/title, sin secretos | 401; application/problem+json; todas las aserciones aprobadas | PASS |
| QA02-06 | Alumno no es Administrador | /api/auth/administrador | GET | Fixture en memoria; institutionalStudentToken | Sin cuerpo; token del perfil indicado | 403; status/title, sin secretos | 403; application/problem+json; todas las aserciones aprobadas | PASS |
| QA02-07 | Docente no es Administrador | /api/auth/administrador | GET | Fixture en memoria; institutionalTeacherToken | Sin cuerpo; token del perfil indicado | 403; status/title, sin secretos | 403; application/problem+json; todas las aserciones aprobadas | PASS |
| QA02-08 | Alumno no es Docente | /api/auth/docente | GET | Fixture en memoria; institutionalStudentToken | Sin cuerpo; token del perfil indicado | 403; status/title, sin secretos | 403; application/problem+json; todas las aserciones aprobadas | PASS |
| QA02-09 | JWT institucional no es de Plataforma | /api/plataforma/instituciones | GET | Fixture en memoria; institutionalAdminToken | Sin cuerpo; token del perfil indicado | 401; status/title, sin secretos | 401; application/json; charset=utf-8; todas las aserciones aprobadas | PASS |
| QA02-10 | Login sin campos | /api/auth/login | POST | Fixture en memoria; anónimo; allowMutations=true | {} | 400; ValidationProblemDetails con errors | 400; application/problem+json; charset=utf-8; todas las aserciones aprobadas | PASS |
| QA02-11 | Email inválido | /api/auth/login | POST | Fixture en memoria; anónimo; allowMutations=true | {"email":"no-email","password":"{{qa02Password}}"} | 400; ValidationProblemDetails con errors | 400; application/problem+json; charset=utf-8; todas las aserciones aprobadas | PASS |
| QA02-12 | Rol no permitido | /api/auth/register | POST | Fixture en memoria; institutionalAdminToken; allowMutations=true | {"email":"qa02-new@test.invalid","password":"{{qa02Password}}","nombre":"Prueba","apellido":"Sintética","rol":"Usuario"} | 400; ValidationProblemDetails con errors | 400; application/problem+json; charset=utf-8; todas las aserciones aprobadas | PASS |
| QA02-13 | JSON incompleto | /api/auth/login | POST | Fixture en memoria; anónimo; allowMutations=true | {"email": | 400; ValidationProblemDetails con errors | 400; application/problem+json; charset=utf-8; todas las aserciones aprobadas | PASS |
| QA02-14 | Email numérico | /api/auth/login | POST | Fixture en memoria; anónimo; allowMutations=true | {"email":123,"password":"{{qa02Password}}"} | 400; ValidationProblemDetails con errors | 400; application/problem+json; charset=utf-8; todas las aserciones aprobadas | PASS |
| QA02-15 | Nombre vacío | /api/auth/register | POST | Fixture en memoria; institutionalAdminToken; allowMutations=true | {"email":"qa02-new@test.invalid","password":"{{qa02Password}}","nombre":"","apellido":"Sintética","rol":"Alumno"} | 400; ValidationProblemDetails con errors | 400; application/problem+json; charset=utf-8; todas las aserciones aprobadas | PASS |
| QA02-16 | Email mayor a150 | /api/auth/register | POST | Fixture en memoria; institutionalAdminToken; allowMutations=true | Email mayor a150; resto del DTO válido (ver cases.json) | 400; ValidationProblemDetails con errors | 400; application/problem+json; charset=utf-8; todas las aserciones aprobadas | PASS |
| QA02-17 | Password menor a8 | /api/auth/register | POST | Fixture en memoria; institutionalAdminToken; allowMutations=true | {"email":"qa02-new@test.invalid","password":"{{qa02ShortPassword}}","nombre":"Prueba","apellido":"Sintética","rol":"Alumno"} | 400; ValidationProblemDetails con errors | 400; application/problem+json; charset=utf-8; todas las aserciones aprobadas | PASS |
| QA02-18 | Password mayor a72 bytes UTF8 | /api/auth/register | POST | Fixture en memoria; institutionalAdminToken; allowMutations=true | {"email":"qa02-new@test.invalid","password":"{{qa02LongPassword}}","nombre":"Prueba","apellido":"Sintética","rol":"Alumno"} | 400; ValidationProblemDetails con errors | 400; application/problem+json; charset=utf-8; todas las aserciones aprobadas | PASS |
| QA02-19 | Nombre mayor a100 | /api/auth/register | POST | Fixture en memoria; institutionalAdminToken; allowMutations=true | Nombre mayor a100; resto del DTO válido (ver cases.json) | 400; ValidationProblemDetails con errors | 400; application/problem+json; charset=utf-8; todas las aserciones aprobadas | PASS |
| QA02-20 | Apellido mayor a100 | /api/auth/register | POST | Fixture en memoria; institutionalAdminToken; allowMutations=true | Apellido mayor a100; resto del DTO válido (ver cases.json) | 400; ValidationProblemDetails con errors | 400; application/problem+json; charset=utf-8; todas las aserciones aprobadas | PASS |
| QA02-21 | Institución enviada por cliente | /api/auth/register | POST | Fixture en memoria; institutionalAdminToken; allowMutations=true | {"email":"qa02-new@test.invalid","password":"{{qa02Password}}","nombre":"Prueba","apellido":"Sintética","rol":"Alumno","institucionId":"11111111-1111-1111-1111-111111111111"} | 400; ValidationProblemDetails con errors | 400; application/problem+json; charset=utf-8; todas las aserciones aprobadas | PASS |
| QA02-22 | JSON null | /api/auth/login | POST | Fixture en memoria; anónimo; allowMutations=true | null | 400; ValidationProblemDetails con errors | 400; application/problem+json; charset=utf-8; todas las aserciones aprobadas | PASS |
| QA02-23 | Registro no asigna Administrador | /api/auth/register | POST | Fixture en memoria; institutionalAdminToken; allowMutations=true | {"email":"qa02-new@test.invalid","password":"{{qa02Password}}","nombre":"Prueba","apellido":"Sintética","rol":"Administrador"} | 400; ValidationProblemDetails con errors | 400; application/problem+json; charset=utf-8; todas las aserciones aprobadas | PASS |
| QA02-24 | Rol distingue mayúsculas | /api/auth/register | POST | Fixture en memoria; institutionalAdminToken; allowMutations=true | {"email":"qa02-new@test.invalid","password":"{{qa02Password}}","nombre":"Prueba","apellido":"Sintética","rol":"alumno"} | 400; ValidationProblemDetails con errors | 400; application/problem+json; charset=utf-8; todas las aserciones aprobadas | PASS |
| QA02-25 | Refresh demasiado largo | /api/auth/refresh | POST | Fixture en memoria; anónimo; allowMutations=true | {"refreshToken":"{{qa02LongRefresh}}"} | 400; ValidationProblemDetails con errors | 400; application/problem+json; charset=utf-8; todas las aserciones aprobadas | PASS |
| QA02-26 | Logout demasiado largo | /api/auth/logout | POST | Fixture en memoria; anónimo; allowMutations=true | {"refreshToken":"{{qa02LongRefresh}}"} | 400; ValidationProblemDetails con errors | 400; application/problem+json; charset=utf-8; todas las aserciones aprobadas | PASS |
| QA02-27 | Refresh desconocido | /api/auth/refresh | POST | Fixture en memoria; anónimo; allowMutations=true | {"refreshToken":"{{qa02UnknownRefresh}}"} | 401; status/title, sin secretos | 401; application/problem+json; todas las aserciones aprobadas | PASS |
| QA02-28 | Rol ausente invalida identidad | /api/auth/administrador | GET | Fixture en memoria; qa02MissingRoleToken | Sin cuerpo; token del perfil indicado | 401; status/title, sin secretos | 401; application/problem+json; todas las aserciones aprobadas | PASS |
| QA02-C1 | Control Administrador válido | /api/auth/administrador | GET | Fixture en memoria; institutionalAdminToken | Sin cuerpo; token del perfil indicado | 200; mensaje de recurso protegido | 200; application/json; charset=utf-8; todas las aserciones aprobadas | PASS |
| QA02-C2 | Control Docente válido | /api/auth/docente | GET | Fixture en memoria; institutionalTeacherToken | Sin cuerpo; token del perfil indicado | 200; mensaje de recurso protegido | 200; application/json; charset=utf-8; todas las aserciones aprobadas | PASS |
| QA02-C3 | Control Alumno válido | /api/auth/alumno | GET | Fixture en memoria; institutionalStudentToken | Sin cuerpo; token del perfil indicado | 200; mensaje de recurso protegido | 200; application/json; charset=utf-8; todas las aserciones aprobadas | PASS |
| QA02-C4 | Logout vacío idempotente | /api/auth/logout | POST | Fixture en memoria; anónimo; allowMutations=true | {} | 204; sin cuerpo | 204; None; todas las aserciones aprobadas | PASS |

Las aserciones detalladas y Content-Type obtenido se conservan en `evidencias/qa02/resultado-final.json`. Un PASS exige todas las aserciones, no sólo el código. La suite automatizada comprueba además que las33 solicitudes se ejecutaron y que la guarda deshabilitada envió cero peticiones.
