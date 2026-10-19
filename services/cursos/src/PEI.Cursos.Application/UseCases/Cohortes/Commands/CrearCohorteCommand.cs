using System;

namespace PEI.Cursos.Application.UseCases.Cohortes.Commands;

/* Comando para dar de alta una nueva cohorte bajo un curso existente.
Combina el identificador de curso proveniente de la ruta con los datos enviados en el cuerpo. */
public record CrearCohorteCommand(
    Guid CursoId,
    string Nombre,
    string Periodo,
    Guid DocenteId
);
