using System;

namespace PEI.Cursos.Application.DTOs.Materiales;

/* DTO inmutable que expone el texto extraido del apunte para la vista previa docente y consumo de IA / MCP. */
public record TextoMaterialDto(
    Guid MaterialId,
    string Titulo,
    int CantidadPaginas,
    string Texto,
    bool TieneTexto
);
