using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using PEI.BuildingBlocks.Errors;
using PEI.Cursos.Application.DTOs.Materiales;
using PEI.Cursos.Application.Services;
using PEI.Cursos.Application.UseCases.Materiales.Commands;
using PEI.Cursos.Domain.Entities;
using PEI.Cursos.Domain.Exceptions;
using PEI.Cursos.UnitTests.Common;
using Xunit;

namespace PEI.Cursos.UnitTests.Application.Materiales;

/* Pruebas unitarias para SubirMaterialCommandHandler (S2-CU-01 / Issue #137).
Valida el ciclo de vida completo de subida, extraccion de texto y persistencia desacoplada. */
public class SubirMaterialCommandHandlerTests
{
    private readonly FakeMaterialCursoRepository _materialRepository = new();
    private readonly FakeCursoRepository _cursoRepository = new();
    private readonly FakeCohorteRepository _cohorteRepository = new();
    private readonly FakeAlmacenamientoArchivos _almacenamiento = new();
    private readonly FakeExtractorTextoPdf _extractorTextoPdf = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeTenantContext _tenantContext = new();
    private readonly AutorizacionMaterialService _autorizacionService;
    private readonly SubirMaterialCommandHandler _handler;

    private readonly Guid _institucionId = Guid.NewGuid();
    private readonly Guid _cursoId = Guid.NewGuid();
    private readonly Guid _docenteId = Guid.NewGuid();

    public SubirMaterialCommandHandlerTests()
    {
        _tenantContext.InstitucionId = _institucionId;
        _tenantContext.UsuarioId = _docenteId;
        _tenantContext.Rol = "Docente";

        // Configurar curso y cohorte activa para el docente
        var curso = new Curso(_institucionId, "Sistemas Distribuidos", "Informatica");
        curso.AsignarId(_cursoId);
        _cursoRepository.Items.Add(curso);

        var cohorte = new Cohorte(_institucionId, _cursoId, _docenteId, "Comision 1", "2026-2C");
        _cohorteRepository.Items.Add(cohorte);

        _autorizacionService = new AutorizacionMaterialService(_cursoRepository, _cohorteRepository, _tenantContext);

        _handler = new SubirMaterialCommandHandler(
            _materialRepository,
            _autorizacionService,
            _extractorTextoPdf,
            _almacenamiento,
            _unitOfWork,
            _tenantContext
        );
    }

    [Fact]
    public async Task HandleAsync_ConPdfValido_GuardaEnStorageYPersisteFichaListo()
    {
        var contenido = new MemoryStream(Encoding.UTF8.GetBytes("fake pdf content"));
        var command = new SubirMaterialCommand(
            _cursoId,
            "Unidad 2 - Microservicios",
            "unidad2.pdf",
            contenido.Length,
            "application/pdf",
            contenido
        );

        _extractorTextoPdf.CantidadPaginas = 8;
        _extractorTextoPdf.TextoExtraido = "Conceptos de Clean Architecture y microservicios.";

        var resultado = await _handler.HandleAsync(command);

        Assert.NotNull(resultado);
        Assert.Equal("Unidad 2 - Microservicios", resultado.Titulo);
        Assert.Equal("unidad2.pdf", resultado.NombreArchivo);
        Assert.Equal(8, resultado.CantidadPaginas);
        Assert.Equal("Listo", resultado.Estado);
        Assert.Equal(_institucionId, resultado.InstitucionId);
        Assert.Equal(_cursoId, resultado.CursoId);

        // Verifica que se haya guardado en el storage desacoplado
        Assert.Single(_almacenamiento.ArchivosGuardados);
        Assert.Single(_materialRepository.Items);
        Assert.Equal(1, _unitOfWork.CommitsRealizados);
    }

    [Fact]
    public async Task HandleAsync_CuandoTituloEsNulo_UsaNombreArchivoSinExtension()
    {
        var contenido = new MemoryStream(Encoding.UTF8.GetBytes("fake pdf content"));
        var command = new SubirMaterialCommand(
            _cursoId,
            null, // Titulo no provisto
            "patrones-diseno.pdf",
            contenido.Length,
            "application/pdf",
            contenido
        );

        var resultado = await _handler.HandleAsync(command);

        Assert.Equal("patrones-diseno", resultado.Titulo);
    }

    [Fact]
    public async Task HandleAsync_CuandoArchivoNoEsPdf_LanzaUnsupportedMediaTypeException()
    {
        // Contrato OpenAPI: 415 si el formato no es PDF
        var contenido = new MemoryStream(Encoding.UTF8.GetBytes("fake docx content"));
        var command = new SubirMaterialCommand(
            _cursoId,
            "Documento Word",
            "documento.docx",
            contenido.Length,
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            contenido
        );

        await Assert.ThrowsAsync<UnsupportedMediaTypeException>(() =>
            _handler.HandleAsync(command)
        );

        // Garantiza que no se guarde nada en storage ante un 415
        Assert.Empty(_almacenamiento.ArchivosGuardados);
        Assert.Empty(_materialRepository.Items);
    }

    [Fact]
    public async Task HandleAsync_CuandoSupera10MB_LanzaDomainException()
    {
        // RN-MAT-03: Limite maximo de 10 MB
        long tamano11MB = (11 * 1024 * 1024);
        var contenido = new MemoryStream(new byte[10]);

        var command = new SubirMaterialCommand(
            _cursoId,
            "Archivo Pesado",
            "pesado.pdf",
            tamano11MB,
            "application/pdf",
            contenido
        );

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            _handler.HandleAsync(command)
        );

        Assert.Contains("10 MB", ex.Message);
        Assert.Empty(_almacenamiento.ArchivosGuardados);
    }

    [Fact]
    public async Task HandleAsync_CuandoPdfNoTieneTextoSeleccionable_LanzaDomainExceptionYNoGuardaEnStorage()
    {
        // RN-MAT-04: PDF escaneado sin OCR
        var contenido = new MemoryStream(Encoding.UTF8.GetBytes("fake scanned pdf"));
        var command = new SubirMaterialCommand(
            _cursoId,
            "Escaneado",
            "escaneado.pdf",
            contenido.Length,
            "application/pdf",
            contenido
        );

        _extractorTextoPdf.ExcepcionALanzar = new DomainException("El PDF no tiene texto seleccionable. Subí un PDF generado desde un procesador de texto, no escaneado.");

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            _handler.HandleAsync(command)
        );

        Assert.Contains("El PDF no tiene texto seleccionable", ex.Message);

        // Invariante de almacenamiento: Cero archivos huerfanos en storage ante falla de extraccion
        Assert.Empty(_almacenamiento.ArchivosGuardados);
        Assert.Empty(_materialRepository.Items);
    }
}
