using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace PEI.Cursos.Application.Interfaces;

/* Contrato desacoplado para el almacenamiento fisico de archivos (Principio de Inversion de Dependencias - DIP).
Permite persistir, recuperar y eliminar binarios sin acoplar la logica de negocio
a un proveedor de infraestructura particular (disco local en desarrollo, Supabase Storage o Cloudflare R2 en produccion). */
public interface IAlmacenamientoArchivos
{
    // Guarda el stream de un archivo bajo una ruta o clave relativa y retorna la ubicacion normalizada.
    Task<string> GuardarArchivoAsync(Stream contenido, string rutaRelativa, CancellationToken cancellationToken = default);

    // Recupera el stream de lectura del archivo almacenado o null si no existe.
    Task<Stream?> ObtenerArchivoAsync(string rutaRelativa, CancellationToken cancellationToken = default);

    // Ejecuta la eliminacion fisica del archivo en caso de ser necesario.
    Task<bool> EliminarArchivoAsync(string rutaRelativa, CancellationToken cancellationToken = default);
}
