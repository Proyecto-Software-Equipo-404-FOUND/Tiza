# Guion de presentación — Tiza — 27/11/2026

| Campo | Valor |
|---|---|
| Historia | S3-PO-01 — Guion para la presentación del 27/11 |
| Responsable | Tomás Bandiera |
| Revisión | Franco Ávila |
| Límite | 20/11/2026 |
| Rama | `feature/S3-PO-01-descripcion-corta` |
| Duración prevista | 23 min de exposición + 7 min de preguntas |
| Formato | Opciones del sistema primero, demo después |

## 0. Reglas del guion

1. **Primero las opciones, después la demo.** La demo no arranca hasta que Franco termina el bloque 1.
2. **La demo muestra dos rúbricas distintas**, no solo dos capturas.
3. **No se improvisan números.** Las métricas de calidad se leen del último CI o del informe de QA.
4. **Si algo falla en vivo, no se depura en pantalla.** Se aplica el plan B de la sección 7.

### Reparto

| Persona | Rol en la presentación |
|---|---|
| Franco Ávila | Apertura, problemática y propuesta de valor, Opciones del sistema, cierre |
| Ariel Carriego | Cierre, preguntas (si es que los profesores/invitados hacen preguntas técnicas) |
| Nicolás Monasterio | Conduce la demo |

1. En caso de que Nicolás no pueda hacer la demo, otro dev debería tomar el rol de conductor.
2. En caso de hacer falta, se puede incluir a más devs para formar parte de la demo (Por ejemplo, como Alumnos).
3. El cierre lo hacen Franco y Ariel juntos.
4. En caso de que se hagan preguntas técnicas, Ariel responde preguntas técnicas, en caso de ser necesario,
Re-dirige la pregunta a los devs correspondientes para que respondan.

Para la problematica, se puede usar como ejemplo el resultado de las pruebas PISA.
La conduccion de la demo sería mejor que la haga un dev, así no queda sobre-cargado Franco.


## 1. Orden y tiempos

| Tramo | Tiempo | Quién | Qué hace |
|---|---|---|---|
| Apertura | 0:00–5:00 | Franco | Introducción del equipo, Problema, promesa y recorrido de la presentación |
| Bloque 1: opciones del sistema | 5:00–09:00 | Franco | Qué ofrece Tiza, para quién y qué alternativas se evaluaron |
| Bloque 2: demo | 09:00–20:30 | Nicolás (conduce) | Recorrido completo con dos rúbricas (Sistema en vivo) |
| Cierre | 20:30–23:00 | Franco y Ariel | Qué está resuelto y puente a preguntas |
| Preguntas | 23:00–30:00 | Todo el equipo | - |

Los tiempos son aproximados y se ajustan a la velocidad de la presentación.
La idea es que la demo no se corte ni se interrumpa, y que el cierre sea breve.
Adicionalmente, es posible que sea a las 8:00/8:30 de la noche, asi que estirar la presentación no es recomendable.

## 2. Bloque 1 — Opciones del sistema (Franco, 4 min)

**Objetivo:** que los participantes entiendan qué puede hacer el sistema antes de verlo funcionando.

### 2.1 Opciones funcionales

Decir, en este orden y sin entrar en pantallas:

1. **Tres roles, un mismo sistema.** Administrador: alta de usuarios. Docente: cursos, cohortes, matrículas, evaluaciones, sala y resultados. Alumno: ingreso con PIN y respuesta en vivo.
2. **Gestión académica.** Curso → cohorte → matrícula. Todo queda aislado por institución; un usuario de la institución A no ve datos de la B.
3. **Evaluaciones con rúbrica.** Preguntas de opción múltiple y abiertas. Cada pregunta abierta puede tener su propia rúbrica con criterios y puntajes. La evaluación nace como borrador, se edita y recién después se publica en una cohorte.
4. **Sala en vivo.** El docente abre una sala con PIN, capacidad y duración. Los alumnos ingresan, responden y el docente ve quién entró, cuántos respondieron y las alertas de foco.
5. **Corrección asistida por IA.** La IA recibe el enunciado, la respuesta, la rúbrica y el puntaje máximo. Devuelve una nota sugerida, una justificación y el desglose por criterio. **La nota final sigue siendo del docente.**
6. **Resultados.** Listado de entregas, nota de opción múltiple, sugerencia de IA en abiertas, alertas de foco y detalle por alumno.

En el punto 1, en caso de que ya esté implementado, se puede mencionar la posibilidad de que en una sala puede haber
invitados que no sean alumnos de la cohorte.

### 2.2 Alternativas que evaluamos

Mencionar como decisiones, no como defensa técnica:

| Decisión | Alternativa descartada | Por qué |
|---|---|---|
| Microservicios por dominio | Monolito único | Equipos y despliegues separados; una falla de IA no tumba la sala |
| SignalR + Redis | Polling cada X segundos | Latencia y carga; el aula necesita eventos, no consultas repetidas |
| Rúbrica del docente como insumo obligatorio | Pedirle a la IA una nota “libre” | Trazabilidad pedagógica y desglose por criterio |
| Varios proveedores de IA con fallback | Un solo proveedor | Si el principal falla o limita cuota, se conmuta solo |

**Ejemplo de frase de transición**

> “Estas son las opciones que ofrece el sistema. Ahora las vemos funcionando de punta a punta, con dos rúbricas distintas.”

## 3. Bloque 2 — Demo (Nicolás conduce, 11 min 30 s)

### 3.1 Antes de empezar

- Notebook con el entorno ya levantado y **dos navegadores distintos** (o una ventana normal y una de incógnito).
- Sesión de docente en el navegador A. Sesión de alumno en el navegador B.
- Examen demo ya creado y publicado (sección 4.3), con la corrección de IA verificada al menos una vez.
- Resultados de una corrida anterior a mano, por si la IA no responde en vivo.
- Video de respaldo de 2–3 minutos con la demo completa.

Alternativamente, Nicolás podría tener el navegador A como docente,
y otro dev puede estar con el navegador B como Alumno en otro dispositivo.

### 3.2 Paso a paso

| Paso | Tiempo | Pantalla / acción |
|---|---|---|
| D1 | 0:00–0:30 | `/login` → correo `docente@a.test` + contraseña de demo → **Ingresar** |
| D2 | 0:30–2:00 | **Cursos** → *Programación I* → cohorte *Comisión 1A · 2026-2C* → **Matrícula** |
| D3 | 2:00–3:30 | **Evaluaciones** → *Demo final — Programación I* → preguntas 2 y 3 |
| D3b | opcional 15 s | Volver al listado → *Cuestionario de repaso: Bases de datos* (borrador) |
| D4 | 3:30–4:30 | Volver al examen demo → **Abrir sala** → capacidad `10`, duración `15` → **Abrir sala** |
| D5 | 4:30–5:15 | Navegador B: `/login` como `alumno1@a.test` → **Sala en vivo** → PIN que muestra el docente → aceptar aviso de integridad → **Entrar**. Repetir con `alumno2@a.test` en otra ventana o mostrar que el panel suma 2. |
| D6 | 5:15–6:00 | Navegador A: ver “Ingresaron: 2” → **Iniciar** |
| D7 | 6:00–7:15 | Alumno 1 y alumno 2 responden las tres preguntas: la opción múltiple, la abierta de la rúbrica A y la abierta de la rúbrica B. En una de las ventanas, cambiar de pestaña 2 segundos para generar una alerta de foco amarilla, y luego más de 3 segundos para una roja. |
| D8 | 7:15–7:45 | Navegador A: **Siguiente pregunta** hasta la última → **Finalizar** → confirmar |
| D9 | 7:45–8:15 | **Resultados** → dos entregas |
| D10 | 8:15–9:15 | Abrir la entrega de `alumno1@a.test` → mostrar **Nota de la IA**, **Justificación** y **desglose por criterio** de la **Rúbrica A** y de la **Rúbrica B** |
| D11 | 9:15–9:45 | Volver a Resultados → señalar que `alumno2@a.test` obtuvo menos puntaje en esas mismas rúbricas |
| D12 | 9:45–10:30 | Frase de cierre de la demo |
| Buffer | 10:30–11:30 | Margen si la IA tarda o el aula va lenta |

**Regla de oro:** si la IA todavía muestra “Pendiente”, esperar 10–20 segundos una sola vez y seguir. No recargar en bucle ni abrir la consola.

### 3.3 Frases que no se improvisan

- “La IA **sugiere**; la nota final es del docente.”
- “Cada pregunta abierta tiene **su** rúbrica.”
- “Las alertas de foco son un **aviso**, no una sanción.”

## 4. Datos de prueba

### 4.1 Institución y usuarios

| Dato | Valor |
|---|---|
| Institución | Institución de prueba A — `TIZA-TEST-A` — `11111111-1111-1111-1111-111111111111` |
| Docente | `docente@a.test` — `11111111-0000-0000-0000-000000000002` |
| Alumno 1 | `alumno1@a.test` — `11111111-0000-0000-0000-000000000003` |
| Alumno 2 | `alumno2@a.test` — `11111111-0000-0000-0000-000000000004` |
| Administrador | `admin@a.test` — `08000001-0000-4000-8000-000000000001` |

La contraseña de demo se define en el entorno y **no se versiona**. Para el ensayo, acordar una única contraseña temporal y cargarla por variable, igual que en el seed de Identidad. En modo mock, cualquier contraseña no vacía alcanza.

**Origen de estos datos en el repositorio**

| Dato | Archivo |
|---|---|
| Institución, usuarios y roles | `services/identidad/tools/PEI.Identidad.DevelopmentSeed/SeedCatalog.cs` |
| Contraseñas (variable de entorno) | `services/identidad/docs/ID08-DATOS-SEMILLA.md` |
| Cursos, cohortes y matrículas | `services/cursos/src/PEI.Cursos.Infrastructure/Persistence/DataSeeder.cs` |
| Evaluaciones sembradas y Rúbrica A | `services/evaluaciones/src/PEI.Evaluaciones.Infrastructure/Persistence/EvaluacionSeedData.cs` |
| Rúbrica B (solo mocks) | `frontend/src/mocks/evaluaciones.ts` |
| Copia de los UUID en el frontend | `frontend/src/mocks/datos.ts` |

La semilla de Identidad y Cursos corre **solo en Development** y es idempotente: si la base ya tiene registros, no inserta nada. El examen de la demo no está sembrado y se crea desde la UI (sección 4.3).

### 4.2 Cursos y matrículas

| Dato | Valor |
|---|---|
| Curso | *Programación I* — `cccccccc-0000-0000-0000-000000000001` |
| Cohorte | *Comisión 1A · 2026-2C* — `33333333-3333-3333-3333-333333333333` |
| Matrículas | `alumno1@a.test` y `alumno2@a.test` en esa cohorte |
| Docente de la cohorte | `docente@a.test` |

### 4.3 Examen de la demo: *Demo final — Programación I*

Se crea desde la UI antes del ensayo y se publica en *Comisión 1A · 2026-2C*. Total: **16 puntos**.

| # | Tipo | Enunciado | Puntaje | Rúbrica |
|---|---|---|---|---|
| 1 | Opción múltiple | ¿Qué palabra clave de C# declara una constante en tiempo de compilación? (`const`, `readonly`, `static`, `var`) | 2 | Automática |
| 2 | Abierta | Explicá qué es una interfaz en C# y para qué sirve. | 6 | **Rúbrica A — Interfaz en C#** |
| 3 | Abierta | Explicá la diferencia entre una variable y una constante. | 8 | **Rúbrica B — Variables y constantes** |

**Rúbrica A — Interfaz en C# (6 puntos)**

| Criterio | Puntaje |
|---|---|
| Define correctamente qué es una interfaz | 2 |
| Incluye un ejemplo concreto | 2 |
| Justifica para qué se usa en un diseño | 2 |

**Rúbrica B — Variables y constantes (8 puntos)**

| Criterio | Puntaje |
|---|---|
| Define correctamente variable | 3 |
| Define correctamente constante | 3 |
| Da un ejemplo de cada una | 2 |

Estas dos rúbricas ya existen en el repositorio:

- Rúbrica A: pregunta abierta de la semilla publicada *Examen parcial de Programacion I*.
- Rúbrica B: existe en los datos de demo del frontend; para el entorno integrado se crea junto con el examen de la demo.

Para la demo se combinan en un solo examen, así una sola sala aplica **dos rúbricas distintas**.

### 4.4 Respuestas sugeridas para la demo

| Alumno | Pregunta 2 (rúbrica A) | Pregunta 3 (rúbrica B) |
|---|---|---|
| `alumno1@a.test` | “Una interfaz es un contrato que define métodos sin implementarlos. Por ejemplo, `IComparable` permite ordenar objetos. Sirve para depender de abstracciones y tener varias implementaciones.” | “Una variable puede cambiar su valor durante la ejecución; una constante no. Por ejemplo: `int edad = 20;` y `const double PI = 3.14;`.” |
| `alumno2@a.test` | “Una interfaz sirve para conectar cosas.” | “Una constante es una variable que no cambia.” |

Resultado esperado: `alumno1` cerca del puntaje completo y `alumno2` con puntajes parciales. **No leer números exactos** si la IA devuelve otro valor: señalar el desglose y la justificación.

### 4.5 Exámenes ya sembrados (plan B)

| Evaluación | Estado | UUID | Uso |
|---|---|---|---|
| *Examen parcial de Programacion I* | Publicada | `aaaaaaaa-0000-0000-0000-000000000001` | Demo de respaldo con la rúbrica A |
| *Cuestionario de repaso: Bases de datos* | Borrador | `aaaaaaaa-0000-0000-0000-000000000002` | Mostrar el flujo borrador → publicar |

### 4.6 Sala

| Dato | Valor |
|---|---|
| Capacidad | 10 alumnos |
| Duración | 15 minutos |
| PIN | El que genera el sistema; se proyecta |
| Alumnos | 2 (`alumno1@a.test`, `alumno2@a.test`) |

## 5. Cierre (Ariel y Franco, 2 min 30 s)

1. **Qué resolvimos:** evaluación en vivo, rúbricas del docente, corrección sugerida con desglose y devolución el mismo día.
2. **Qué demostramos hoy:** dos rúbricas distintas aplicadas en una misma sala, con resultados explicables.
3. **Qué sigue:** confirmación y edición de la nota final por el docente, exportación de resultados e integraciones institucionales.

## 6. Tarjeta de emergencia: “¿qué no hicieron?”

Respuesta marco, 20 segundos:

> “Tiza cubre el circuito de evaluación en vivo y corrección asistida. Lo que no está en esta entrega lo tenemos identificado y priorizado; no lo presentamos como hecho.”

| No está en esta entrega | Qué sí hay | Próximo paso |
|---|---|---|
| Confirmar o editar la nota final desde la pantalla | Nota sugerida, justificación y desglose por criterio | Acción de aceptar/ajustar y registrar la nota final |
| Exportar notas a CSV/PDF | Resultados consultables en pantalla | Exportación por evaluación y por cohorte |
| Dashboard analítico histórico | Resultados por evaluación | Métricas por curso, cohorte y criterio |
| Integración con Moodle/Classroom | API y contratos propios | Adaptador institucional |
| Escaneo de exámenes en papel u OCR | Evaluación en vivo desde el navegador | Carga de imágenes y OCR |
| SSO institucional y recuperación de contraseña | Login con JWT, refresh y roles | SSO y recuperación autogestionada |

En caso de que pregunten por lo que no hicimos, se puede responder algo parecido a:
“Priorizamos el circuito crítico: evaluación en vivo, rúbricas y corrección sugerida.
Lo que quedó afuera está identificado y tiene un próximo paso definido.”

## 7. Riesgos y planes B

| Riesgo | Cómo se detecta | Plan B | Dueño |
|---|---|---|---|
| La IA no responde o tarda | “Pendiente” por más de 30 s | Mostrar una entrega ya corregida de la corrida previa y explicar el fallback; no esperar en vivo | Nicolás y Ariel o Santiago |
| El examen demo no está publicado | No aparece **Abrir sala** | Usar el examen sembrado *Examen parcial de Programacion I*, que ya tiene la Rúbrica A, y mencionar que la Rúbrica B se crea en la UI | Nicolás |
| Datos sucios de ensayos anteriores | Resultados duplicados o entregas viejas | Correr la demo con usuarios y examen nuevos; no borrar datos en vivo | Nicolás |
| Se pasa el tiempo | Voz de aviso a los 18 min | Saltar D3b y D11; cerrar con resultados y dos rúbricas | Ariel |
