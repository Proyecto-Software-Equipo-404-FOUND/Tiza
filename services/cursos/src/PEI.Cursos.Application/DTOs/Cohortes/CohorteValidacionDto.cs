using System;

namespace PEI.Cursos.Application.DTOs.Cohortes;

/* Contrato inmutable de validacion de cohorte expuesto para consultas inter-servicio.
Provee a Evaluaciones los datos minimos para verificar pertenencia institucional y docente titular. */
public record CohorteValidacionDto(
    Guid CohorteId,
    Guid InstitucionId,
    Guid CursoId,
    Guid DocenteId,
    string Nombre,
    bool Activa
);
