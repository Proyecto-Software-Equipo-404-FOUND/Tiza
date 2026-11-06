using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using PEI.BuildingBlocks.Errors;
using PEI.Cursos.Application.Services;
using PEI.Cursos.Application.UseCases.Materiales.Queries;
using PEI.Cursos.Domain.Entities;
using PEI.Cursos.UnitTests.Common;
using Xunit;

namespace PEI.Cursos.UnitTests.Application.Materiales;

/* Pruebas unitarias para ObtenerTextoMaterialQueryHandler.
Valida el acceso defensivo para el consumo interno de IA / MCP. */
public class ObtenerTextoMaterialQueryHandlerTests
{
    private readonly FakeMaterialCursoRepository _materialRepository = new();
    private readonly FakeCursoRepository _cursoRepository = new();
    private readonly FakeCohorteRepository _cohorteRepository = new();
    private readonly FakeTenantContext _tenantContext = new();
    private readonly AutorizacionMaterialService _autorizacionService;
    private readonly ObtenerTextoMaterialQueryHandler _handler;

    private readonly Guid _institucionId = Guid.NewGuid();
    private readonly Guid _cursoId = Guid.NewGuid();
    private readonly Guid _docenteId = Guid.NewGuid();

    public ObtenerTextoMaterialQueryHandlerTests()
    {
        _tenantContext.InstitucionId = _institucionId;
        _tenantContext.Rol = "Docente";

        _autorizacionService = new AutorizacionMaterialService(_cursoRepository, _cohorteRepository, _tenantContext);
        _handler = new ObtenerTextoMaterialQueryHandler(_materialRepository, _autorizacionService);
    }

    [Fact]
    public async Task HandleAsync_CuandoMaterialExisteYEstaListo_RetornaTextoYMetadatos()
    {
        var material = new MaterialCurso(
            _institucionId,
            _cursoId,
            "Resumen IA",
            "resumen.pdf",
            1024,
            15,
            "path/resumen.pdf",
            "Texto completo para generar preguntas de examen.",
            _docenteId
        );
        _materialRepository.Items.Add(material);

        var query = new ObtenerTextoMaterialQuery(material.Id);

        var resultado = await _handler.HandleAsync(query);

        Assert.NotNull(resultado);
        Assert.Equal(material.Id, resultado.MaterialId);
        Assert.Equal("Resumen IA", resultado.Titulo);
        Assert.Equal(15, resultado.CantidadPaginas);
        Assert.Equal("Texto completo para generar preguntas de examen.", resultado.Texto);
    }

    [Fact]
    public async Task HandleAsync_CuandoUsuarioEsAlumno_LanzaForbiddenException()
    {
        // RN-MAT-02: El alumno no accede al texto
        _tenantContext.Rol = "Alumno";

        var query = new ObtenerTextoMaterialQuery(Guid.NewGuid());

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            _handler.HandleAsync(query)
        );
    }

    [Fact]
    public async Task HandleAsync_CuandoMaterialNoExiste_LanzaKeyNotFoundException()
    {
        var query = new ObtenerTextoMaterialQuery(Guid.NewGuid());

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _handler.HandleAsync(query)
        );
    }

    [Fact]
    public async Task HandleAsync_CuandoMaterialEstaInactivo_LanzaKeyNotFoundException()
    {
        var material = new MaterialCurso(
            _institucionId,
            _cursoId,
            "Inactivo",
            "inactivo.pdf",
            1024,
            5,
            "path/inactivo.pdf",
            "Texto",
            _docenteId
        );
        material.Desactivar();
        _materialRepository.Items.Add(material);

        var query = new ObtenerTextoMaterialQuery(material.Id);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _handler.HandleAsync(query)
        );
    }
}
