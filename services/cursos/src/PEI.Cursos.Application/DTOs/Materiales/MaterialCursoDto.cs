using System;

namespace PEI.Cursos.Application.DTOs.Materiales;

/* DTO inmutable que representa la ficha de un apunte de curso segun el contrato OpenAPI v1.
Omite el texto completo del documento para optimizar la transferencia en listados y creacion. */
public record MaterialCursoDto(
    Guid Id,
    Guid InstitucionId,
    Guid CursoId,
    string Titulo,
    string NombreArchivo,
    long TamanoBytes,
    int CantidadPaginas,
    string Estado,
    Guid SubidoPorId,
    DateTime FechaSubidaUtc
);
