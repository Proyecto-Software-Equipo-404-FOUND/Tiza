using System;

namespace PEI.Cursos.Application.DTOs.Cursos;

/* Contrato de lectura para la entidad Curso expuesto hacia la capa de API.
Evita exponer directamente la entidad de dominio protegiendo su encapsulamiento. */
public record CursoDto(
    Guid Id,
    Guid InstitucionId,
    string Nombre,
    string Area,
    bool Activo,
    DateTime FechaCreacionUtc
);
