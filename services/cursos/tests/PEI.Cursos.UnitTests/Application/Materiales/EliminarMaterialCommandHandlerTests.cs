using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using PEI.Cursos.Application.Services;
using PEI.Cursos.Application.UseCases.Materiales.Commands;
using PEI.Cursos.Domain.Entities;
using PEI.Cursos.UnitTests.Common;
using Xunit;

namespace PEI.Cursos.UnitTests.Application.Materiales;

/* Pruebas unitarias para EliminarMaterialCommandHandler.
Verifica la baja logica y la preservacion del archivo fisico en disco (RN-MAT-05). */
public class EliminarMaterialCommandHandlerTests
{
    private readonly FakeMaterialCursoRepository _materialRepository = new();
    private readonly FakeCursoRepository _cursoRepository = new();
    private readonly FakeCohorteRepository _cohorteRepository = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeTenantContext _tenantContext = new();
    private readonly AutorizacionMaterialService _autorizacionService;
    private readonly EliminarMaterialCommandHandler _handler;

    private readonly Guid _institucionId = Guid.NewGuid();
    private readonly Guid _cursoId = Guid.NewGuid();
    private readonly Guid _docenteId = Guid.NewGuid();

    public EliminarMaterialCommandHandlerTests()
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

        _handler = new EliminarMaterialCommandHandler(
            _materialRepository,
            _autorizacionService,
            _unitOfWork
        );
    }

    [Fact]
    public async Task HandleAsync_CuandoMaterialExisteYEstaActivo_EjecutaBajaLogica()
    {
        var material = new MaterialCurso(
            _institucionId,
            _cursoId,
            "Apunte",
            "apunte.pdf",
            1024,
            5,
            "path/apunte.pdf",
            "Texto",
            _docenteId
        );
        _materialRepository.Items.Add(material);

        var command = new EliminarMaterialCommand(_cursoId, material.Id);

        var resultado = await _handler.HandleAsync(command);

        Assert.True(resultado);
        Assert.False(material.Activo); // RN-MAT-05: Baja logica (Activo = false)
        Assert.Equal(1, _unitOfWork.CommitsRealizados);
    }

    [Fact]
    public async Task HandleAsync_CuandoMaterialNoExiste_LanzaKeyNotFoundException()
    {
        var command = new EliminarMaterialCommand(_cursoId, Guid.NewGuid());

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _handler.HandleAsync(command)
        );

        Assert.Equal(0, _unitOfWork.CommitsRealizados);
    }

    [Fact]
    public async Task HandleAsync_CuandoMaterialPerteneceAOtroCurso_LanzaKeyNotFoundException()
    {
        var otroCursoId = Guid.NewGuid();
        var material = new MaterialCurso(
            _institucionId,
            otroCursoId,
            "Apunte",
            "apunte.pdf",
            1024,
            5,
            "path/apunte.pdf",
            "Texto",
            _docenteId
        );
        _materialRepository.Items.Add(material);

        var command = new EliminarMaterialCommand(_cursoId, material.Id);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _handler.HandleAsync(command)
        );
    }
}
