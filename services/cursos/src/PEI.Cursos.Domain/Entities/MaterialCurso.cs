using System;
using PEI.Cursos.Domain.Common;
using PEI.Cursos.Domain.Enums;
using PEI.Cursos.Domain.Exceptions;

namespace PEI.Cursos.Domain.Entities;

/* Entidad de Dominio rica que representa un apunte o material de estudio en formato PDF.
Encapsula los metadatos del documento, el texto extraido para IA y las reglas de negocio
asociadas al tamano maximo (10 MB), cantidad de paginas (30) y ciclo de vida (RN-MAT-01 a RN-MAT-05). */
public class MaterialCurso : BaseEntity
{
    public const long LimiteTamanoBytesMaximo = 10 * 1024 * 1024; // 10 MB (RN-MAT-03)
    public const int LimitePaginasMaximo = 30; // 30 paginas (RN-MAT-03)
    public const int LimiteTituloLongitud = 150;

    public Guid CursoId { get; private set; }
    public string Titulo { get; private set; } = null!;
    public string NombreArchivo { get; private set; } = null!;
    public long TamanoBytes { get; private set; }
    public int CantidadPaginas { get; private set; }
    public string UbicacionArchivo { get; private set; } = null!;
    public string TextoExtraido { get; private set; } = null!;
    public EstadoMaterialCurso Estado { get; private set; }
    public Guid SubidoPorId { get; private set; }
    public DateTime FechaSubidaUtc { get; private set; }
    public bool Activo { get; private set; }

    // Propiedad de navegacion virtual hacia el curso contenedor para Entity Framework Core.
    public virtual Curso Curso { get; private set; } = null!;

    /* Constructor de negocio. Exige las invariantes requeridas y garantiza
    que el material solo se instancie con datos consistentes y texto extraido. */
    public MaterialCurso(
        Guid institucionId,
        Guid cursoId,
        string titulo,
        string nombreArchivo,
        long tamanoBytes,
        int cantidadPaginas,
        string ubicacionArchivo,
        string textoExtraido,
        Guid subidoPorId)
    {
        if (institucionId == Guid.Empty)
            throw new DomainException("La institucion es obligatoria para registrar un material.");

        if (cursoId == Guid.Empty)
            throw new DomainException("El curso es obligatorio para asociar un material.");

        if (subidoPorId == Guid.Empty)
            throw new DomainException("El identificador del usuario que sube el material es obligatorio.");

        ValidarInvariantes(titulo, nombreArchivo, tamanoBytes, cantidadPaginas, ubicacionArchivo, textoExtraido);

        InstitucionId = institucionId;
        CursoId = cursoId;
        Titulo = titulo.Trim();
        NombreArchivo = nombreArchivo.Trim();
        TamanoBytes = tamanoBytes;
        CantidadPaginas = cantidadPaginas;
        UbicacionArchivo = ubicacionArchivo.Trim();
        TextoExtraido = textoExtraido;
        Estado = EstadoMaterialCurso.Listo;
        SubidoPorId = subidoPorId;
        FechaSubidaUtc = DateTime.UtcNow;
        Activo = true;
    }

    // Constructor sin parametros requerido exclusivamente por EF Core para materializacion.
    private MaterialCurso()
    {
    }

    // Ejecuta la baja logica del apunte preservando el archivo fisico para auditoria e IA (RN-MAT-05).
    public void Desactivar()
    {
        if (!Activo)
            throw new DomainException("El material ya se encuentra inactivo.");

        Activo = false;
        FechaModificacionUtc = DateTime.UtcNow;
    }

    // Validador centralizado de invariantes y limites tecnicos del documento.
    private static void ValidarInvariantes(
        string titulo,
        string nombreArchivo,
        long tamanoBytes,
        int cantidadPaginas,
        string ubicacionArchivo,
        string textoExtraido)
    {
        if (string.IsNullOrWhiteSpace(titulo))
            throw new DomainException("El titulo del material es obligatorio.");

        if (titulo.Trim().Length > LimiteTituloLongitud)
            throw new DomainException($"El titulo del material no puede superar los {LimiteTituloLongitud} caracteres.");

        if (string.IsNullOrWhiteSpace(nombreArchivo))
            throw new DomainException("El nombre del archivo es obligatorio.");

        if (tamanoBytes <= 0)
            throw new DomainException("El tamano del archivo debe ser mayor a cero bytes.");

        if (tamanoBytes > LimiteTamanoBytesMaximo)
            throw new DomainException("El archivo supera el tamano maximo permitido de 10 MB.");

        if (cantidadPaginas <= 0)
            throw new DomainException("El documento debe contener al menos una pagina.");

        if (cantidadPaginas > LimitePaginasMaximo)
            throw new DomainException($"El PDF supera el limite permitido de {LimitePaginasMaximo} paginas.");

        if (string.IsNullOrWhiteSpace(ubicacionArchivo))
            throw new DomainException("La ubicacion fisica del archivo en storage es obligatoria.");

        if (string.IsNullOrWhiteSpace(textoExtraido))
            throw new DomainException("El PDF no tiene texto seleccionable. Subi un PDF generado desde un procesador de texto, no escaneado.");
    }
}
