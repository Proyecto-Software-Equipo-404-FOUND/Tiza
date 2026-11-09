using System;

namespace PEI.Cursos.Application.DTOs.Materiales;

/* DTO inmutable que expone el texto extraido del apunte para consumo interno de IA y herramientas MCP. */
public record TextoMaterialDto(
    Guid MaterialId,
    string Titulo,
    int CantidadPaginas,
    string Texto
);
