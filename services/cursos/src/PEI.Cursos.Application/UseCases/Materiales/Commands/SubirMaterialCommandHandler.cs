using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using PEI.BuildingBlocks.Errors;
using PEI.BuildingBlocks.MultiTenancy;
using PEI.Cursos.Application.DTOs.Materiales;
using PEI.Cursos.Application.Interfaces;
using PEI.Cursos.Domain.Entities;
using PEI.Cursos.Domain.Exceptions;

namespace PEI.Cursos.Application.UseCases.Materiales.Commands;

/* Manejador del comando SubirMaterialCommand (Caso de Uso S2-CU-01).
Orquesta la verificacion de permisos, validacion del stream, extraccion de texto con PdfPig,
almacenamiento desacoplado en disco y persistencia de la ficha en PostgreSQL. */
public class SubirMaterialCommandHandler : IRequestHandler<SubirMaterialCommand, MaterialCursoDto>
{
    private readonly IMaterialCursoRepository _materialRepository;
    private readonly IAutorizacionMaterialService _autorizacionService;
    private readonly IExtractorTextoPdf _extractorTextoPdf;
    private readonly IAlmacenamientoArchivos _almacenamientoArchivos;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITenantContext _tenantContext;

    public SubirMaterialCommandHandler(
        IMaterialCursoRepository materialRepository,
        IAutorizacionMaterialService autorizacionService,
        IExtractorTextoPdf extractorTextoPdf,
        IAlmacenamientoArchivos almacenamientoArchivos,
        IUnitOfWork unitOfWork,
        ITenantContext tenantContext)
    {
        _materialRepository = materialRepository;
        _autorizacionService = autorizacionService;
        _extractorTextoPdf = extractorTextoPdf;
        _almacenamientoArchivos = almacenamientoArchivos;
        _unitOfWork = unitOfWork;
        _tenantContext = tenantContext;
    }

    public async Task<MaterialCursoDto> HandleAsync(SubirMaterialCommand request, CancellationToken cancellationToken = default)
    {
        // 1. Validar identidad institucional y de usuario en el contexto autenticado
        var institucionId = _tenantContext.InstitucionId;
        var usuarioId = _tenantContext.UsuarioId;
        if (!institucionId.HasValue || institucionId.Value == Guid.Empty || !usuarioId.HasValue || usuarioId.Value == Guid.Empty)
        {
            throw new DomainException("No se pudo identificar la institucion o el usuario autenticado.");
        }

        // 2. Validar autorizacion de acceso sobre el curso (404 anti-enumeracion / 403 permisos)
        await _autorizacionService.ValidarPermisoGestionAsync(request.CursoId, cancellationToken);

        // 3. Validar formato PDF segun extension y MIME type (415 Unsupported Media Type)
        var extension = Path.GetExtension(request.NombreArchivo);
        var esPdf = string.Equals(extension, ".pdf", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(request.ContentType, "application/pdf", StringComparison.OrdinalIgnoreCase);

        if (!esPdf)
        {
            throw new UnsupportedMediaTypeException("El archivo debe estar en formato PDF.");
        }

        // 4. Validar tamano maximo del archivo (10 MB - RN-MAT-03)
        if (request.TamanoBytes <= 0)
        {
            throw new DomainException("El tamano del archivo debe ser mayor a cero bytes.");
        }

        if (request.TamanoBytes > MaterialCurso.LimiteTamanoBytesMaximo)
        {
            throw new DomainException("El archivo supera el tamano maximo permitido de 10 MB.");
        }

        // 5. Determinar titulo visible (si no se especifica se toma el nombre del archivo sin extension)
        var tituloFinal = string.IsNullOrWhiteSpace(request.Titulo)
            ? Path.GetFileNameWithoutExtension(request.NombreArchivo)
            : request.Titulo.Trim();

        // 6. Extraccion de texto y validacion de paginas en memoria (RN-MAT-03 y RN-MAT-04)
        var extraccion = await _extractorTextoPdf.ExtraerTextoAsync(request.ArchivoStream, cancellationToken);

        // 7. Almacenamiento fisico desacoplado en storage bajo clave organizada
        var materialId = Guid.NewGuid();
        var rutaRelativa = $"{institucionId.Value}/{request.CursoId}/{materialId}.pdf";

        if (request.ArchivoStream.CanSeek)
        {
            request.ArchivoStream.Position = 0;
        }

        var ubicacionNormalizada = await _almacenamientoArchivos.GuardarArchivoAsync(
            request.ArchivoStream,
            rutaRelativa,
            cancellationToken
        );

        // 8. Instanciacion de la entidad de dominio y persistencia
        var material = new MaterialCurso(
            institucionId.Value,
            request.CursoId,
            tituloFinal,
            request.NombreArchivo,
            request.TamanoBytes,
            extraccion.CantidadPaginas,
            ubicacionNormalizada,
            extraccion.TextoExtraido,
            usuarioId.Value
        );

        material.AsignarId(materialId);

        await _materialRepository.AddAsync(material, cancellationToken);
        await _unitOfWork.CommitAsync(cancellationToken);

        // 9. Proyeccion al DTO de salida
        return new MaterialCursoDto(
            material.Id,
            material.InstitucionId,
            material.CursoId,
            material.Titulo,
            material.NombreArchivo,
            material.TamanoBytes,
            material.CantidadPaginas,
            material.Estado.ToString(),
            material.SubidoPorId,
            material.FechaSubidaUtc
        );
    }
}
