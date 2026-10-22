using System;

namespace PEI.Cursos.Application.DTOs.Cohortes;

/* Contrato de salida inmutable para la entidad Cohorte expuesto a la API.
Comunica la comision o division asociada a un curso y un docente responsable. */
public record CohorteDto(
    Guid Id,
    Guid CursoId,
    Guid DocenteId,
    string Nombre,
    string Periodo,
    bool Activo,
    DateTime FechaCreacionUtc
);
