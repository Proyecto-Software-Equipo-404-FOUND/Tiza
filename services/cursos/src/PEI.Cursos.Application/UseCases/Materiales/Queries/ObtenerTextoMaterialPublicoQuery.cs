using System;

namespace PEI.Cursos.Application.UseCases.Materiales.Queries;

/* Consulta para obtener el texto extraido de un apunte con visibilidad publica (docente / administrador).
Permite confirmar el contenido previo a la lectura de la IA (S3-CU-03 / Issue #229). */
public record ObtenerTextoMaterialPublicoQuery(Guid CursoId, Guid MaterialId);
