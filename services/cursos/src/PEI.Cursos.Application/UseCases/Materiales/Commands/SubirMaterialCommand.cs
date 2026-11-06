using System;
using System.IO;

namespace PEI.Cursos.Application.UseCases.Materiales.Commands;

/* Comando para la subida de un apunte PDF y procesamiento de texto (S2-CU-01).
Recibe el identificador del curso de la ruta URL y los datos binarios del archivo del multipart. */
public record SubirMaterialCommand(
    Guid CursoId,
    string? Titulo,
    string NombreArchivo,
    long TamanoBytes,
    string ContentType,
    Stream ArchivoStream
);
