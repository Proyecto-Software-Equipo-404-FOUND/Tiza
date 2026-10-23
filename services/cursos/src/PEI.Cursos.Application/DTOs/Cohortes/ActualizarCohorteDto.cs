using System;

namespace PEI.Cursos.Application.DTOs.Cohortes;

/* DTO de entrada para la actualizacion de datos de una cohorte existente (PUT).
Recibe exclusivamente los atributos mutables de la comision (nombre, periodo y docente asignado). */
public record ActualizarCohorteDto(
    string Nombre,
    string Periodo,
    Guid DocenteId
);
