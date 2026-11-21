using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using PEI.BuildingBlocks.Errors;
using PEI.Cursos.Application.Services;
using PEI.Cursos.Application.UseCases.Materiales.Queries;
using PEI.Cursos.Domain.Entities;
using PEI.Cursos.Domain.Enums;
using PEI.Cursos.UnitTests.Common;
using Xunit;

namespace PEI.Cursos.UnitTests.Application.Materiales;

/* Pruebas unitarias para ObtenerTextoMaterialPublicoQueryHandler (Caso de Uso S3-CU-03 / Issue #229).
Verifica los criterios de aceptacion: visibilidad para docente y administrador, bloqueo a alumnos y ajenos,
deteccion de PDFs sin texto seleccionable (TieneTexto = false), regla anti-enumeracion y estados no listos. */
public class ObtenerTextoMaterialPublicoQueryHandlerTests
{
    private readonly FakeMaterialCursoRepository _materialRepository = new();
    private readonly FakeCursoRepository _cursoRepository = new();
    private readonly FakeCohorteRepository _cohorteRepository = new();
    private readonly FakeTenantContext _tenantContext = new();
    private readonly AutorizacionMaterialService _autorizacionService;
    private readonly ObtenerTextoMaterialPublicoQueryHandler _handler;

    private readonly Guid _institucionId = Guid.NewGuid();
    private readonly Guid _cursoId = Guid.NewGuid();
    private readonly Guid _docenteId = Guid.NewGuid();

    public ObtenerTextoMaterialPublicoQueryHandlerTests()
    {
        _tenantContext.InstitucionId = _institucionId;
        _tenantContext.UsuarioId = _docenteId;
        _tenantContext.Rol = "Docente";

        // Curso base perteneciente a la institucion del tenant
        var curso = new Curso(_institucionId, "Arquitectura de Software", "Sistemas");
        curso.AsignarId(_cursoId);
        _cursoRepository.Items.Add(curso);

        // Cohorte base que vincula al docente con el curso
        var cohorte = new Cohorte(_institucionId, _cursoId, _docenteId, "Comision A", "2026-2C");
        _cohorteRepository.Items.Add(cohorte);

        _autorizacionService = new AutorizacionMaterialService(_cursoRepository, _cohorteRepository, _tenantContext);
        _handler = new ObtenerTextoMaterialPublicoQueryHandler(_materialRepository, _autorizacionService);
    }

    [Fact]
    public async Task HandleAsync_DocenteAsignadoConTexto_RetornaDtoConTextoYBanderaTrue()
    {
        var material = new MaterialCurso(
            _institucionId,
            _cursoId,
            "Unidad 1 - Clean Architecture",
            "unidad1.pdf",
            2048,
            12,
            "storage/unidad1.pdf",
            "Texto extraido completo con conceptos de Clean Architecture.",
            _docenteId
        );
        _materialRepository.Items.Add(material);

        var query = new ObtenerTextoMaterialPublicoQuery(_cursoId, material.Id);

        var resultado = await _handler.HandleAsync(query);

        Assert.NotNull(resultado);
        Assert.Equal(material.Id, resultado.MaterialId);
        Assert.Equal("Unidad 1 - Clean Architecture", resultado.Titulo);
        Assert.Equal(12, resultado.CantidadPaginas);
        Assert.Equal("Texto extraido completo con conceptos de Clean Architecture.", resultado.Texto);
        Assert.True(resultado.TieneTexto);
    }

    [Fact]
    public async Task HandleAsync_Administrador_RetornaDtoExitosamente()
    {
        _tenantContext.Rol = "Administrador";

        var material = new MaterialCurso(
            _institucionId,
            _cursoId,
            "Manual Institucional",
            "manual.pdf",
            1024,
            5,
            "storage/manual.pdf",
            "Contenido accesible para el administrador.",
            _docenteId
        );
        _materialRepository.Items.Add(material);

        var query = new ObtenerTextoMaterialPublicoQuery(_cursoId, material.Id);

        var resultado = await _handler.HandleAsync(query);

        Assert.NotNull(resultado);
        Assert.Equal("Manual Institucional", resultado.Titulo);
        Assert.True(resultado.TieneTexto);
    }

    [Fact]
    public async Task HandleAsync_PdfSinTexto_RetornaTieneTextoFalseYTextoVacio()
    {
        // Simula apunte escaneado sin texto seleccionable
        var material = CrearMaterialConTexto(_institucionId, _cursoId, _docenteId, "");
        _materialRepository.Items.Add(material);

        var query = new ObtenerTextoMaterialPublicoQuery(_cursoId, material.Id);

        var resultado = await _handler.HandleAsync(query);

        Assert.NotNull(resultado);
        Assert.False(resultado.TieneTexto);
        Assert.Equal(string.Empty, resultado.Texto);
    }

    [Fact]
    public async Task HandleAsync_DocenteNoAsignadoAlCurso_LanzaForbiddenException()
    {
        // Docente autenticado pero sin comisiones asignadas a este curso
        _tenantContext.UsuarioId = Guid.NewGuid();

        var material = new MaterialCurso(
            _institucionId,
            _cursoId,
            "Apunte Ajeno",
            "apunte.pdf",
            1024,
            4,
            "storage/apunte.pdf",
            "Texto protegido.",
            _docenteId
        );
        _materialRepository.Items.Add(material);

        var query = new ObtenerTextoMaterialPublicoQuery(_cursoId, material.Id);

        await Assert.ThrowsAsync<ForbiddenException>(() => _handler.HandleAsync(query));
    }

    [Fact]
    public async Task HandleAsync_UsuarioAlumno_LanzaForbiddenException()
    {
        // RN-MAT-02: El alumno tiene prohibido el acceso a materiales y a su texto
        _tenantContext.Rol = "Alumno";

        var query = new ObtenerTextoMaterialPublicoQuery(_cursoId, Guid.NewGuid());

        await Assert.ThrowsAsync<ForbiddenException>(() => _handler.HandleAsync(query));
    }

    [Fact]
    public async Task HandleAsync_CursoNoExiste_LanzaKeyNotFoundException()
    {
        // Regla anti-enumeracion: curso inexistente o ajeno
        var cursoInexistenteId = Guid.NewGuid();
        var query = new ObtenerTextoMaterialPublicoQuery(cursoInexistenteId, Guid.NewGuid());

        await Assert.ThrowsAsync<KeyNotFoundException>(() => _handler.HandleAsync(query));
    }

    [Fact]
    public async Task HandleAsync_MaterialNoExiste_LanzaKeyNotFoundException()
    {
        var materialInexistenteId = Guid.NewGuid();
        var query = new ObtenerTextoMaterialPublicoQuery(_cursoId, materialInexistenteId);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => _handler.HandleAsync(query));
    }

    [Fact]
    public async Task HandleAsync_MaterialDeOtroCurso_LanzaKeyNotFoundException()
    {
        // El material existe pero pertenece a otro curso (anti-enumeracion / consistencia jerarquica)
        var otroCursoId = Guid.NewGuid();
        var materialOtroCurso = new MaterialCurso(
            _institucionId,
            otroCursoId,
            "Apunte de otro curso",
            "otro.pdf",
            1024,
            6,
            "storage/otro.pdf",
            "Texto de otro curso.",
            _docenteId
        );
        _materialRepository.Items.Add(materialOtroCurso);

        var query = new ObtenerTextoMaterialPublicoQuery(_cursoId, materialOtroCurso.Id);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => _handler.HandleAsync(query));
    }

    [Fact]
    public async Task HandleAsync_MaterialInactivo_LanzaKeyNotFoundException()
    {
        // Material dado de baja logica
        var material = new MaterialCurso(
            _institucionId,
            _cursoId,
            "Apunte dado de baja",
            "baja.pdf",
            1024,
            8,
            "storage/baja.pdf",
            "Texto inactivo.",
            _docenteId
        );
        material.Desactivar();
        _materialRepository.Items.Add(material);

        var query = new ObtenerTextoMaterialPublicoQuery(_cursoId, material.Id);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => _handler.HandleAsync(query));
    }

    [Fact]
    public async Task HandleAsync_MaterialNoListo_LanzaInvalidOperationException()
    {
        // Contrato OpenAPI: si todavia se procesa emite 409 Conflict (MATERIAL_NO_LISTO)
        var material = CrearMaterialEnEstado(_institucionId, _cursoId, _docenteId, EstadoMaterialCurso.Procesando);
        _materialRepository.Items.Add(material);

        var query = new ObtenerTextoMaterialPublicoQuery(_cursoId, material.Id);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _handler.HandleAsync(query));
        Assert.Contains("procesando", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // Metodos auxiliares para emular escenarios de persistencia y estado
    private static MaterialCurso CrearMaterialConTexto(Guid institucionId, Guid cursoId, Guid subidoPorId, string textoExtraido)
    {
        var material = new MaterialCurso(
            institucionId,
            cursoId,
            "Apunte Sin Texto",
            "escaneado.pdf",
            1024,
            2,
            "storage/escaneado.pdf",
            "Texto temporal",
            subidoPorId
        );

        typeof(MaterialCurso).GetProperty(nameof(MaterialCurso.TextoExtraido))!
            .SetValue(material, textoExtraido);

        return material;
    }

    private static MaterialCurso CrearMaterialEnEstado(Guid institucionId, Guid cursoId, Guid subidoPorId, EstadoMaterialCurso estado)
    {
        var material = new MaterialCurso(
            institucionId,
            cursoId,
            "Apunte En Proceso",
            "proceso.pdf",
            1024,
            3,
            "storage/proceso.pdf",
            "Texto en proceso",
            subidoPorId
        );

        typeof(MaterialCurso).GetProperty(nameof(MaterialCurso.Estado))!
            .SetValue(material, estado);

        return material;
    }
}
