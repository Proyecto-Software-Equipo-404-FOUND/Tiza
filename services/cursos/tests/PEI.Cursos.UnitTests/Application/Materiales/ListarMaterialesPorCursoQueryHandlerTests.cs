using System;
using System.Threading.Tasks;
using PEI.Cursos.Application.Services;
using PEI.Cursos.Application.UseCases.Materiales.Queries;
using PEI.Cursos.Domain.Entities;
using PEI.Cursos.UnitTests.Common;
using Xunit;

namespace PEI.Cursos.UnitTests.Application.Materiales;

/* Pruebas unitarias para ListarMaterialesPorCursoQueryHandler. */
public class ListarMaterialesPorCursoQueryHandlerTests
{
    private readonly FakeMaterialCursoRepository _materialRepository = new();
    private readonly FakeCursoRepository _cursoRepository = new();
    private readonly FakeCohorteRepository _cohorteRepository = new();
    private readonly FakeTenantContext _tenantContext = new();
    private readonly AutorizacionMaterialService _autorizacionService;
    private readonly ListarMaterialesPorCursoQueryHandler _handler;

    private readonly Guid _institucionId = Guid.NewGuid();
    private readonly Guid _cursoId = Guid.NewGuid();
    private readonly Guid _docenteId = Guid.NewGuid();

    public ListarMaterialesPorCursoQueryHandlerTests()
    {
        _tenantContext.InstitucionId = _institucionId;
        _tenantContext.UsuarioId = _docenteId;
        _tenantContext.Rol = "Docente";

        var curso = new Curso(_institucionId, "Sistemas Distribuidos", "Informatica");
        curso.AsignarId(_cursoId);
        _cursoRepository.Items.Add(curso);

        var cohorte = new Cohorte(_institucionId, _cursoId, _docenteId, "Comision 1", "2026-2C");
        _cohorteRepository.Items.Add(cohorte);

        _autorizacionService = new AutorizacionMaterialService(_cursoRepository, _cohorteRepository, _tenantContext);

        _handler = new ListarMaterialesPorCursoQueryHandler(
            _materialRepository,
            _autorizacionService
        );
    }

    [Fact]
    public async Task HandleAsync_RetornaSoloMaterialesActivosDelCurso()
    {
        var mat1 = new MaterialCurso(_institucionId, _cursoId, "M1", "m1.pdf", 100, 2, "p1", "t1", _docenteId);
        var mat2 = new MaterialCurso(_institucionId, _cursoId, "M2", "m2.pdf", 100, 3, "p2", "t2", _docenteId);
        var matInactivo = new MaterialCurso(_institucionId, _cursoId, "M3", "m3.pdf", 100, 4, "p3", "t3", _docenteId);
        matInactivo.Desactivar();

        _materialRepository.Items.Add(mat1);
        _materialRepository.Items.Add(mat2);
        _materialRepository.Items.Add(matInactivo);

        var query = new ListarMaterialesPorCursoQuery(_cursoId);

        var resultado = await _handler.HandleAsync(query);

        Assert.Equal(2, resultado.Count);
        Assert.DoesNotContain(resultado, m => m.Titulo == "M3");
    }
}
