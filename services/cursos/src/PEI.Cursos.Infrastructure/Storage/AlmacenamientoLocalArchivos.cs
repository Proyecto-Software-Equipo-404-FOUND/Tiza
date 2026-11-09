using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using PEI.Cursos.Application.Interfaces;

namespace PEI.Cursos.Infrastructure.Storage;

/* Implementacion de IAlmacenamientoArchivos para almacenamiento en el sistema de archivos local.
Resuelve la ruta fisica a partir de una carpeta base configurable (Almacenamiento:DirectorioBase)
y almacena los documentos bajo claves relativas normalizadas ({institucionId}/{cursoId}/{materialId}.pdf). */
public class AlmacenamientoLocalArchivos : IAlmacenamientoArchivos
{
    private readonly string _directorioBase;

    public AlmacenamientoLocalArchivos(IConfiguration configuration)
    {
        var configPath = configuration["Almacenamiento:DirectorioBase"];
        _directorioBase = string.IsNullOrWhiteSpace(configPath)
            ? Path.Combine(AppContext.BaseDirectory, "storage", "materiales")
            : configPath;
    }

    public async Task<string> GuardarArchivoAsync(Stream contenido, string rutaRelativa, CancellationToken cancellationToken = default)
    {
        var rutaNormalizada = NormalizarRutaRelativa(rutaRelativa);
        var rutaFisicaCompleta = Path.Combine(_directorioBase, rutaNormalizada);

        var directorioPadre = Path.GetDirectoryName(rutaFisicaCompleta);
        if (!string.IsNullOrEmpty(directorioPadre) && !Directory.Exists(directorioPadre))
        {
            Directory.CreateDirectory(directorioPadre);
        }

        if (contenido.CanSeek)
        {
            contenido.Position = 0;
        }

        using var fileStream = new FileStream(
            rutaFisicaCompleta,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 81920,
            useAsync: true
        );

        await contenido.CopyToAsync(fileStream, cancellationToken);

        // Retorna la clave relativa usando barras universales para compatibilidad con S3 y URLs
        return rutaNormalizada.Replace('\\', '/');
    }

    public Task<Stream?> ObtenerArchivoAsync(string rutaRelativa, CancellationToken cancellationToken = default)
    {
        var rutaNormalizada = NormalizarRutaRelativa(rutaRelativa);
        var rutaFisicaCompleta = Path.Combine(_directorioBase, rutaNormalizada);

        if (!File.Exists(rutaFisicaCompleta))
        {
            return Task.FromResult<Stream?>(null);
        }

        Stream fileStream = new FileStream(
            rutaFisicaCompleta,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 81920,
            useAsync: true
        );

        return Task.FromResult<Stream?>(fileStream);
    }

    public Task<bool> EliminarArchivoAsync(string rutaRelativa, CancellationToken cancellationToken = default)
    {
        var rutaNormalizada = NormalizarRutaRelativa(rutaRelativa);
        var rutaFisicaCompleta = Path.Combine(_directorioBase, rutaNormalizada);

        if (File.Exists(rutaFisicaCompleta))
        {
            File.Delete(rutaFisicaCompleta);
            return Task.FromResult(true);
        }

        return Task.FromResult(false);
    }

    private static string NormalizarRutaRelativa(string ruta)
    {
        return ruta.Replace('/', Path.DirectorySeparatorChar)
                   .Replace('\\', Path.DirectorySeparatorChar)
                   .TrimStart(Path.DirectorySeparatorChar);
    }
}
