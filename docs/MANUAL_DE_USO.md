# Manual de uso de Tiza

**Versión:** 1.1  
**Fecha:** 17/11/2026  
**Alcance:** uso funcional de la aplicación web por rol. Este documento no explica la instalación ni la configuración técnica del entorno; para eso está la guía del `README.md` y la documentación de cada servicio.

---

## 1. ¿Qué es Tiza?

Tiza es la plataforma de evaluación institucional de Tiza (proyecto **PEI**). Permite:

- Organizar cursos, cohortes y matrículas.
- Crear evaluaciones con preguntas de opción múltiple y preguntas abiertas.
- Publicar una evaluación en una cohorte.
- Tomar la evaluación en vivo: el docente abre una sala con un PIN y conduce las preguntas; los alumnos responden desde la misma plataforma.
- Revisar las entregas y las notas sugeridas por la IA.

La IA **sugiere** una nota para las preguntas abiertas a partir de la rúbrica, pero la decisión final de la nota es del docente. Las alertas de foco (salidas de pantalla del alumno) son solo un aviso y no modifican la nota.

---

## 2. Roles y accesos

| Rol | Cómo obtiene la cuenta | Qué puede hacer |
| :--- | :--- | :--- |
| **Administrador** | Cuenta creada por la institución | Crear usuarios, cursos y cohortes; matricular alumnos; ver y editar cursos y cohortes; publicar evaluaciones y consultar resultados. **No puede abrir ni conducir salas en vivo.** |
| **Docente** | Cuenta creada por un administrador | Ver cursos, cohortes y matrículas; subir apuntes; crear, editar y publicar evaluaciones; abrir y conducir salas en vivo; revisar resultados y entregas. |
| **Alumno** | Cuenta creada por un administrador y matrícula en una cohorte | Entrar a la sala en vivo con un PIN y responder la evaluación. No ve cursos, evaluaciones ni notas desde el menú. |
| **Invitado** | Sin cuenta. El docente le habilita un acceso a una sala puntual. | Entrar únicamente a la sala habilitada, aceptar el aviso de integridad y responder la evaluación. No accede a cursos, evaluaciones, notas ni al resto de la plataforma. |

> **Importante:** Tiza no tiene autorregistro. Un alumno sin cuenta puede rendir únicamente si un docente le habilita el acceso a una sala puntual (ver sección 4). Ese acceso no crea una cuenta ni una matrícula, no sirve para otras salas y no da acceso al resto de la plataforma.

Cada usuario pertenece a una institución. Solo ve los cursos, evaluaciones y usuarios de su propia institución.

---

## 3. Acceso a la plataforma

### 3.1. Iniciar sesión

1. Abrir la dirección de Tiza que indique la institución (en desarrollo local, `http://localhost:3000`).
2. En la pantalla **Ingresar a Tiza**, completar:
   - **Correo:** el email con el que se creó la cuenta.
   - **Contraseña:** la contraseña inicial entregada por el administrador.
3. Si hace falta, usar el ícono del ojo para mostrar u ocultar la contraseña.
4. Presionar **Ingresar**.

Todos los roles entran a la pantalla **Inicio**. Desde el menú lateral se accede a cada sección.

Si aparece **«Correo o contraseña incorrectos»**, revisar ambos datos. Por seguridad, el sistema no indica cuál de los dos es incorrecto.

### 3.2. Cerrar sesión

1. Presionar **Salir** en la esquina superior derecha.
2. La sesión se cierra y se vuelve al login. Los datos cargados en pantalla se descartan.

### 3.3. Recuperar contraseña

La aplicación no tiene recuperación automática de contraseña. Si un usuario la olvidó, debe pedirle a un administrador de su institución que le cree una cuenta nueva o le asigne una contraseña inicial.

---

## 4. Invitado: alumno sin cuenta en una sala puntual

Un alumno sin cuenta puede rendir una evaluación **solo** si un docente le habilita el acceso a **una sala puntual**. Ese acceso:

- No crea una cuenta ni una matrícula.
- Sirve únicamente para la sala habilitada (una evaluación y una cohorte determinadas).
- Puede estar asociado a una persona y revocarse antes de que ingrese.
- Vence cuando la sala se cierra o cuando se agota el tiempo de la sala.
- No da acceso a cursos, evaluaciones, resultados ni al resto de la plataforma.

### 4.1. Requisitos

- La evaluación debe estar publicada.
- El docente debe haber abierto la sala.
- El docente debe habilitar el acceso del alumno sin cuenta.
- El alumno debe aceptar el aviso de integridad antes de entrar.

### 4.2. Cómo lo habilita el docente

1. Abrir la evaluación publicada y entrar a la sala (ver sección 6.5).
2. En el panel de la sala, elegir la opción **Habilitar alumno sin cuenta**. El nombre exacto puede variar en la interfaz final.
3. Indicar el **nombre o identificador** con el que el alumno va a figurar en la sala y en los resultados. Conviene usar el nombre real o un identificador acordado con la institución.
4. Confirmar la habilitación. La plataforma genera un acceso para esa sala, por ejemplo un enlace o un código de un solo uso.
5. Entregar el acceso al alumno por el medio acordado. No se debe publicar en un canal abierto.
6. Si el alumno no debe ingresar, revocar el acceso desde el mismo panel antes de que lo use.

> El acceso no se reutiliza en otra sala. Si el docente abre una sala nueva, debe habilitar un acceso nuevo.

### 4.3. Cómo entra el alumno sin cuenta

1. Abrir el enlace de acceso recibido o ingresar el código, según la implementación. **No debe iniciar sesión.**
2. Confirmar el nombre con el que va a rendir.
3. Leer el aviso **Antes de empezar** y marcar **Entendí el aviso y acepto**. Se registra si sale de la pantalla y no se usa la cámara ni el micrófono.
4. Presionar **Entrar**.
5. Esperar a que el docente inicie la evaluación.
6. Responder cada pregunta como cualquier alumno (ver secciones 7.3 a 7.7).

### 4.4. Qué ve el docente

- El alumno aparece en la lista de la sala con el nombre indicado y una marca de **invitado** o **sin cuenta**.
- Se contabiliza en los alumnos conectados y en los que respondieron la pregunta actual.
- Sus respuestas y sus alertas de foco quedan asociadas a esa entrega.
- En **Resultados**, la entrega se identifica como invitado y conserva el nombre indicado.

### 4.5. Límites y seguridad

- El acceso es válido solo para la sala habilitada.
- No permite elegir otra evaluación, entrar a otra cohorte ni acceder a otras pantallas.
- No crea usuario, contraseña ni matrícula.
- El docente es responsable de entregar el acceso a la persona correcta y de revocarlo si hace falta.
- Las respuestas del invitado no se vinculan a una cuenta institucional.
- La capacidad máxima y la duración de la sala siguen aplicando.
- Las alertas de foco se registran igual que para un alumno con cuenta y no modifican la nota.

### 4.6. Diferencia entre un alumno con cuenta y un alumno sin cuenta

| Aspecto | Alumno con cuenta | Alumno sin cuenta |
| :--- | :--- | :--- |
| Cómo entra | Inicia sesión y usa el PIN de la sala | Usa un acceso habilitado por el docente |
| Matrícula | Debe estar matriculado en la cohorte | No requiere matrícula |
| Alcance | Todas las salas de sus cohortes | Solo la sala habilitada |
| Historial | Queda asociado a su cuenta | Queda asociado a la entrega de esa sala |
| Acceso a notas | No tiene pantalla de notas en esta versión | No tiene pantalla de notas |
| Vigencia | Mientras tenga cuenta y matrícula | Hasta que se cierra la sala o se revoca el acceso |

### 4.7. Si el alumno necesita acceso permanente

Si el alumno va a cursar habitualmente, conviene darle una cuenta:

1. Un administrador crea un usuario con rol **Alumno** desde **Usuarios**.
2. Lo matricula en la cohorte desde **Cursos → Ver cohortes → Ver matrícula**.
3. El alumno recibe el correo y la contraseña inicial y ya puede iniciar sesión.

El acceso puntual sin cuenta está pensado para casos excepcionales, no para reemplazar la matriculación.

---

## 5. Guía del administrador

El menú del administrador muestra **Inicio**, **Cursos**, **Evaluaciones** y **Usuarios**.

### 5.1. Crear usuarios

1. Ir a **Usuarios** en el menú.
2. En la tarjeta **Crear usuario**, elegir el **Rol**:
   - **Alumno:** para que luego se lo matricule en una cohorte.
   - **Docente:** para asignarlo a una cohorte.
3. Completar:
   - **Nombre** y **Apellido** (obligatorios, hasta 100 caracteres cada uno).
   - **Email** y **Repetir email** (deben coincidir, hasta 150 caracteres).
   - **Contraseña inicial** y **Repetir contraseña** (mínimo 8 caracteres, máximo 72 bytes).
4. Presionar **Crear alumno** o **Crear docente**.

Al finalizar, el usuario aparece en **Usuarios de la institución** y recibe un aviso con el siguiente paso:

- Si es alumno: «Ya podés matricularlo desde la cohorte».
- Si es docente: «Ya podés asignarlo a una cohorte».

Si el email ya existe, se muestra **«Ya existe un usuario con ese email»**.

### 5.2. Crear un curso

1. Ir a **Cursos**.
2. En la tarjeta **Crear nuevo curso**, completar:
   - **Nombre del curso** (obligatorio, hasta 100 caracteres).
   - **Área** (obligatoria, hasta 50 caracteres). Se puede elegir una sugerencia o escribir una nueva.
3. Presionar **Crear curso**.

El curso aparece en **Tus cursos activos**.

### 5.3. Editar o dar de baja un curso

1. En **Tus cursos activos**, ubicar el curso.
2. Usar las acciones de la columna **Acciones**:
   - **Editar:** abre una ventana para cambiar los datos.
   - **Eliminar:** pide confirmación. El curso pasa a **Cursos inactivos**.

La baja es lógica: el curso no se borra definitivamente. Los cursos inactivos se muestran en solo lectura y ya no se pueden elegir para publicar evaluaciones.

### 5.4. Crear una cohorte y asignarle un docente

1. Ir a **Cursos** y presionar **Ver cohortes** en el curso correspondiente.
2. En la tarjeta **Nueva cohorte**, completar:
   - **Nombre de la cohorte** (obligatorio, hasta 100 caracteres).
   - **Período** (obligatorio, hasta 30 caracteres; por ejemplo, `2026-1C`).
   - **Docente:** elegir uno de la lista de docentes de la institución.
3. Presionar **Crear cohorte**.

Si no hay docentes disponibles, primero hay que crearlos desde **Usuarios**.

### 5.5. Editar o dar de baja una cohorte

1. En la pantalla de cohortes del curso, ubicar la cohorte.
2. Usar **Acciones → Editar** o **Acciones → Eliminar**.
3. Confirmar la baja si corresponde. La cohorte pasa a **Cohortes inactivas** y ya no se puede elegir para publicar evaluaciones.

### 5.6. Matricular alumnos

1. Ir a **Cursos → Ver cohortes → Ver matrícula** de la cohorte.
2. En la tarjeta **Matricular un alumno**, elegir un alumno de la lista.
3. Presionar **Matricular**.

La lista solo muestra alumnos que todavía no están matriculados en esa cohorte. Si no queda ninguno, se indica **«No quedan alumnos para matricular»**.

Para dar de baja a un alumno:

1. En **Alumnos matriculados**, presionar **Dar de baja**.
2. Confirmar con **Sí, dar de baja**.

El alumno pasa a **Matrículas inactivas** y deja de poder entrar a las salas en vivo de esa cohorte. Para reincorporarlo, presionar **Reactivar** en esa sección.

### 5.7. Subir apuntes a un curso

1. Ir a **Cursos → Ver apuntes** en el curso correspondiente.
2. En la tarjeta **Subir apunte**, elegir un archivo **PDF de hasta 10 MB**.
3. Opcionalmente, escribir un **Título**. Si se deja vacío, se usa el nombre del archivo.
4. Presionar **Subir apunte**.

La tabla muestra título, páginas, tamaño, fecha, estado (*Procesando*, *Listo* o *Con error*) y la acción **Eliminar**. Para eliminar un apunte, presionar **Eliminar** y confirmar; la acción no se puede deshacer.

### 5.8. Evaluaciones y resultados

El administrador puede:

- Ver todas las evaluaciones de la institución en **Evaluaciones**.
- Entrar al detalle de una evaluación.
- Publicar un borrador en una cohorte.
- Consultar **Resultados** y el detalle de cada entrega.

Lo que **no** puede hacer es abrir o conducir una sala en vivo: esa acción es exclusiva del rol docente.

---

## 6. Guía del docente

El menú del docente muestra **Inicio**, **Cursos** y **Evaluaciones**.

### 6.1. Consultar cursos, cohortes y matrículas

1. Ir a **Cursos**.
2. Presionar **Ver cohortes** para ver las cohortes del curso.
3. Presionar **Ver matrícula** para ver los alumnos matriculados.

El docente puede consultar esta información, pero **no** crear, editar ni eliminar cursos o cohortes: esas acciones son del administrador.

### 6.2. Subir apuntes

El docente puede subir y eliminar apuntes de un curso siguiendo los mismos pasos del administrador (sección 5.7).

### 6.3. Crear una evaluación (borrador)

1. Ir a **Evaluaciones**.
2. Presionar **Nueva evaluación**.
3. En **Datos generales**, completar:
   - **Título** (obligatorio, hasta 200 caracteres).
   - **Tipo:** *Examen*, *Cuestionario* o *Quiz*.
4. En **Preguntas**, presionar **Agregar pregunta** y completar cada pregunta:
   - **Enunciado** (obligatorio).
   - **Tipo de pregunta:** *Opción múltiple* o *Abierta*.
   - **Puntaje máximo:** número de 0 o más.
5. Según el tipo de pregunta:
   - **Opción múltiple:** agregar al menos 2 opciones, escribir el texto de cada una y marcar con el botón de radio cuál es la correcta.
   - **Abierta:** la rúbrica es opcional. Se pueden agregar criterios con descripción y puntaje. Si se agrega una rúbrica, **los criterios deben sumar exactamente el puntaje máximo** de la pregunta; la pantalla muestra cuánto falta o cuánto sobra. Si no se agrega rúbrica, la IA corrige solo con el puntaje máximo.
6. Repetir para todas las preguntas necesarias.
7. Presionar **Guardar borrador**.

El sistema guarda la evaluación como **Borrador** y abre su detalle. También se puede guardar un borrador sin preguntas y completarlo después.

Para modificar un borrador:

1. Abrir la evaluación desde **Evaluaciones**.
2. Presionar **Editar borrador**.
3. Hacer los cambios y guardar.

Para borrar un borrador: en el detalle, usar **Eliminar borrador** y confirmar. Solo se pueden borrar borradores.

### 6.4. Publicar una evaluación

1. Abrir el detalle de un borrador.
2. En la tarjeta **Publicar evaluación**, elegir:
   - **Curso:** solo aparecen los cursos activos.
   - **Cohorte:** solo aparecen las cohortes activas del curso elegido.
3. Presionar **Publicar**.

Requisitos y consecuencias:

- La evaluación debe tener al menos una pregunta.
- Debe elegirse una cohorte.
- **Una vez publicada, la evaluación ya no se puede editar ni borrar.**

Si la evaluación cambió en el servidor mientras se estaba editando, puede aparecer un aviso; en ese caso, recargar y revisar el estado actual antes de volver a intentar.

### 6.5. Abrir una sala en vivo

1. Abrir una evaluación **publicada**.
2. Presionar **Abrir sala**.
3. Completar:
   - **Capacidad máxima:** entre 1 y 100 alumnos (valor por defecto: 40).
   - **Duración en minutos:** entre 5 y 240 minutos (valor por defecto: 60).
4. Presionar **Abrir sala**.

Se crea una sala con un **PIN de 6 dígitos**. El tiempo empieza a correr al abrir la sala; al vencer, ya no se puede entrar ni responder.

La pantalla del docente muestra:

- El **PIN** en grande, para proyectarlo.
- La **capacidad máxima**.
- Los cronómetros de la sala y, si corresponde, de la pregunta.
- La cantidad de alumnos conectados y cuántos respondieron la pregunta actual.
- La lista de alumnos con su peor severidad de alerta de foco.

> Si el docente recarga la página, la sala se recupera desde el almacenamiento del navegador para esa evaluación. Para abrir otra sala, usar **Abrir otra sala**; la sala anterior sigue vigente, pero se deja de conducir desde ese panel.

### 6.6. Habilitar el ingreso de un alumno sin cuenta

Permite que un alumno que no tiene cuenta ni matrícula pueda rendir **una sala puntual**, sin crearle un usuario.

1. Abrir la evaluación **publicada** y entrar a la sala (ver sección 6.5).
2. En el panel de la sala, elegir **Habilitar alumno sin cuenta**.
3. Escribir el **nombre o identificador** con el que el alumno va a figurar en la sala y en los resultados.
4. Confirmar. La plataforma genera un acceso válido **solo para esa sala** (enlace o código de un solo uso, según la implementación).
5. Entregar el acceso al alumno por el medio acordado. No compartirlo en un canal abierto.
6. Si el alumno no debe entrar, usar **Revocar acceso** antes de que lo utilice.

Al ingresar, el alumno aparece en la lista de la sala con una marca de **invitado** o **sin cuenta**. Se contabiliza en la capacidad y en las respuestas de la pregunta actual.

> El acceso no sirve para otra sala ni para otra evaluación. Si se abre una sala nueva, hay que habilitar un acceso nuevo. El acceso vence al cerrarse la sala o al agotarse su tiempo.

### 6.7. Conducir la sala

1. Esperar a que los alumnos ingresen con el PIN o con el acceso habilitado para invitados.
2. Cuando estén listos, presionar **Iniciar**.
3. Mientras la evaluación está en curso, el docente ve la pregunta actual y cuántos alumnos respondieron.
4. Presionar **Siguiente pregunta** para avanzar. El botón se habilita cuando corresponde.
5. Al terminar, presionar **Finalizar** y confirmar con **Sí, finalizar**.

Al finalizar, los alumnos dejan de poder responder y se entregan los exámenes. Si se agota el tiempo, la sala se cierra automáticamente.

> El contador de alumnos solo suma. Si se recarga la página, se recupera a medida que los alumnos vuelven a entrar.

### 6.8. Revisar resultados y entregas

1. Abrir la evaluación publicada.
2. Presionar la pestaña **Resultados**.
3. La tabla **Entregas** muestra, por alumno:
   - **Estado** de la entrega.
   - **Múltiple choice:** nota obtenida sobre el máximo.
   - **Nota de la IA:** sugerencia para las preguntas abiertas, marcada como sugerida. Si la IA todavía no corrigió, aparece **Pendiente**; si el alumno no respondió nada, aparece **Sin respuestas**.
   - **Alertas de foco:** cantidad y severidad.
   - **Entregó:** fecha y hora.
   - Si la entrega es de un **alumno sin cuenta**, se identifica como invitado y se muestra el nombre indicado al habilitar el acceso.
4. Presionar el nombre del alumno para abrir el detalle de la entrega.

En el detalle se ve:

- Las notas de múltiple choice y de abiertas (IA).
- Las alertas de foco.
- Cada respuesta del alumno, con indicación de correcta o incorrecta en las de opción múltiple.
- En las abiertas: la respuesta, la nota sugerida por la IA, su justificación y el desglose por criterio de la rúbrica.
- La sección **Salidas de la pantalla**, con gravedad, duración y momento de cada alerta.

> La nota de la IA es **solo una sugerencia**: no es la nota final. Las alertas de foco no modifican la nota.

---

## 7. Guía del alumno

El menú del alumno muestra **Inicio** y **Sala en vivo**.

> Si no tenés cuenta y el docente te habilitó un acceso a una sala puntual, seguí los pasos de la sección 4.3 en lugar de esta sección.

### 7.1. Antes de entrar

El alumno necesita:

- Una cuenta creada por el administrador.
- Estar matriculado en la cohorte de la evaluación.
- El **PIN de 6 dígitos** que proyecta el docente.

### 7.2. Entrar a la sala

1. Iniciar sesión y ir a **Sala en vivo**.
2. Escribir el **PIN de 6 dígitos**. Solo se aceptan números.
3. Leer el aviso **Antes de empezar** y marcar **Entendí el aviso y acepto**. El aviso indica que se registra si el alumno sale de la pantalla y que **no se usa la cámara ni el micrófono**.
4. Presionar **Entrar**.

Si el PIN es incorrecto, incompleto o la sala ya cerró, se muestra un mensaje de error. Si no se acepta el aviso, no se puede entrar.

> **Alumno sin cuenta:** no debe iniciar sesión ni usar el PIN. Debe abrir el acceso que le entregó el docente, confirmar su nombre y aceptar el aviso (ver sección 4.3). El resto de la experiencia dentro de la sala es igual.

### 7.3. Esperar el inicio

Al entrar, aparece **Estás en la sala** con el mensaje de espera. No hay que cerrar la pestaña: cuando el docente inicie la evaluación, aparece la primera pregunta.

### 7.4. Responder

Cada pregunta muestra:

- El número de pregunta y el total.
- El puntaje máximo.
- Un cronómetro.
- El enunciado.
- El campo de respuesta según el tipo.

Para responder:

- **Opción múltiple:** elegir una de las opciones.
- **Abierta:** escribir la respuesta en el cuadro de texto (hasta 10.000 caracteres).

Luego presionar **Enviar respuesta**.

Cada pregunta se responde **una sola vez**. Después de enviar, la tarjeta indica **«Tu respuesta fue enviada»** y hay que esperar a que el docente pase a la siguiente pregunta o cierre la sala.

Si se termina el tiempo de la pregunta o de la sala, no se puede enviar la respuesta y se muestra **«Se terminó el tiempo para responder»**.

### 7.5. Problemas de conexión

- Si se corta la conexión mientras está en la sala, no hay que cerrar la pestaña. Las respuestas ya enviadas quedan guardadas.
- Al volver la conexión, la plataforma intenta llevar al alumno a la pregunta en curso.
- Si una respuesta queda en espera, se envía automáticamente cuando vuelve la conexión.
- Si la reconexión no funciona, se puede recargar la página y volver a entrar con el PIN.

### 7.6. Salidas de pantalla

Durante la evaluación se registra si el alumno sale de la pantalla. Es solo un aviso para el docente:

- **Alerta amarilla:** salida de menos de 3 segundos.
- **Alerta roja:** salida de 3 segundos o más.

Estas alertas **no bajan la nota automáticamente** y no se usa la cámara ni el micrófono.

### 7.7. Al finalizar

Cuando el docente cierra la sala o se termina el tiempo, el alumno ve **Examen entregado**, con el motivo: **«Tu docente cerró la sala»** o **«Se terminó el tiempo»**.

> En esta versión, el alumno no tiene una pantalla para consultar su nota. La revisión de resultados la realiza el docente desde **Evaluaciones → Resultados**.

---

## 8. Flujo completo de una evaluación

Este es el recorrido mínimo para tomar una evaluación de principio a fin.

### Antes de la clase (administrador)

1. Crear las cuentas de docentes y alumnos en **Usuarios**.
2. Crear el curso en **Cursos**.
3. Crear la cohorte y asignarle el docente.
4. Matricular a los alumnos en la cohorte.
5. Opcional: subir apuntes en PDF al curso.

### Antes de la clase (docente)

1. Crear la evaluación y sus preguntas en **Evaluaciones → Nueva evaluación**.
2. Verificar que las rúbricas sumen el puntaje máximo de cada pregunta abierta.
3. Publicar la evaluación en la cohorte correspondiente.
4. Si hay alumnos sin cuenta, definir con qué nombre o identificador van a figurar en los resultados.

### Durante la clase

1. El docente abre la sala desde la evaluación publicada y proyecta el PIN.
2. Si hay alumnos sin cuenta, el docente habilita un acceso para cada uno y se lo entrega por un medio privado (ver sección 6.6).
3. Los alumnos con cuenta entran a **Sala en vivo**, ingresan el PIN y aceptan el aviso. Los alumnos sin cuenta entran con el acceso habilitado y también aceptan el aviso.
4. El docente presiona **Iniciar**.
5. Los alumnos responden cada pregunta.
6. El docente avanza con **Siguiente pregunta**.
7. Al terminar, el docente presiona **Finalizar** y confirma.

### Después de la clase

1. El docente abre **Evaluaciones → Resultados**.
2. Revisa las notas de múltiple choice, las sugerencias de la IA para las abiertas y las alertas de foco.
3. Entra al detalle de cada entrega para ver respuestas, justificaciones y desglose por criterio. Las entregas de alumnos sin cuenta aparecen identificadas como invitados.

---

## 9. Preguntas frecuentes y límites actuales

**¿Un invitado puede entrar a una sala con un PIN?**  
Puede, si el docente le habilitó un acceso a esa sala puntual. No alcanza con el PIN: el acceso lo genera el docente, sirve solo para esa sala y no crea cuenta ni matrícula.

**¿El alumno sin cuenta queda como alumno de la institución?**  
No. El acceso no crea usuario, contraseña ni matrícula. Si necesita acceso permanente, un administrador debe crearle una cuenta y matricularlo.

**¿Puede usar el mismo acceso para otra sala?**  
No. El acceso vale únicamente para la sala habilitada. Si se abre otra sala, el docente debe generar un acceso nuevo.

**¿Puedo registrarme solo?**  
No. Las cuentas las crea un administrador de la institución.

**¿Puedo recuperar mi contraseña?**  
No hay recuperación automática. Hay que pedirle a un administrador que gestione la cuenta.

**¿El administrador puede abrir una sala?**  
No. La sala en vivo es exclusiva del docente.

**¿Se puede editar una evaluación publicada?**  
No. Una vez publicada, no se puede editar ni borrar. Solo los borradores se pueden modificar o eliminar.

**¿La nota de la IA es la nota final?**  
No. Es una sugerencia basada en la rúbrica. La nota final la decide el docente.

**¿Las alertas de foco bajan la nota?**  
No. Son un aviso para el docente y no modifican la nota.

**¿Se graba cámara o micrófono?**  
No. Solo se registra si el alumno sale de la pantalla durante el examen.

**¿Qué pasa si se elimina un curso o una cohorte?**  
La baja es lógica: pasan a una sección de inactivos, se pueden consultar en solo lectura y ya no se pueden elegir para publicar evaluaciones.

**¿Qué formatos de apuntes se aceptan?**  
Solo PDF, de hasta 10 MB.

**¿Dónde ve el alumno sus notas?**  
En esta versión, no hay una pantalla de notas para el alumno. Los resultados los consulta el docente.

**¿Los datos de otra institución son visibles?**  
No. Cada usuario solo accede a la información de su propia institución.

---

## 10. Mapa de pantallas por rol

| Pantalla | Administrador | Docente | Alumno | Invitado |
| :--- | :---: | :---: | :---: | :---: |
| Login | ✓ | ✓ | ✓ | ✓ |
| Inicio | ✓ | ✓ | ✓ | — |
| Cursos | ✓ | ✓ | — | — |
| Cohortes | ✓ | ✓ | — | — |
| Matrícula | ✓ | ✓ | — | — |
| Apuntes | ✓ | ✓ | — | — |
| Evaluaciones | ✓ | ✓ | — | — |
| Detalle de evaluación | ✓ | ✓ | — | — |
| Resultados y entregas | ✓ | ✓ | — | — |
| Sala en vivo (conducir) | — | ✓ | — | — |
| Sala en vivo (responder) | — | — | ✓ | ✓ (solo con acceso habilitado) |
| Habilitar o revocar acceso de alumno sin cuenta | — | ✓ | — | — |
| Usuarios | ✓ | — | — | — |

---

*Manual funcional de Tiza. Para la puesta en marcha del entorno de desarrollo, consultar el `README.md` del repositorio y la documentación de infraestructura.*
