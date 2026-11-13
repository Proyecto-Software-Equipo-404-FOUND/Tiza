using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using PEI.BuildingBlocks.Errors;
using PEI.Cursos.Application.Services;
using PEI.Cursos.Domain.Entities;
using PEI.Cursos.UnitTests.Common;
using Xunit;

namespace PEI.Cursos.UnitTests.Application.Materiales;

/* Pruebas unitarias para el servicio de autorizacion de materiales.
Cubre las reglas de control de acceso y aislamiento institucional (RN-MAT-01 y RN-MAT-02). */
public class AutorizacionMaterialServiceTests
{
    private readonly FakeCursoRepository _cursoRepository = new();
    private readonly FakeCohorteRepository _cohorteRepository = new();
    private readonly FakeTenantContext _tenantContext = new();
    private readonly AutorizacionMaterialService _service;

    private readonly Guid _institucionId = Guid.NewGuid();
    private readonly Guid _cursoId = Guid.NewGuid();
    private readonly Guid _docenteId = Guid.NewGuid();

    public AutorizacionMaterialServiceTests()
    {
        _tenantContext.InstitucionId = _institucionId;
        _service = new AutorizacionMaterialService(_cursoRepository, _cohorteRepository, _tenantContext);
    }

    [Fact]
    public async Task ValidarPermisoGestion_CuandoCursoNoExiste_LanzaKeyNotFoundException()
    {
        // Regla anti-enumeracion: 404 Not Found si no existe o es de otra institucion
        _tenantContext.Rol = "Administrador";

        var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _service.ValidarPermisoGestionAsync(_cursoId)
        );

        Assert.Contains(_cursoId.ToString(), ex.Message);
    }

    [Fact]
    public async Task ValidarPermisoGestion_CuandoCursoEstaInactivo_LanzaKeyNotFoundException()
    {
        var cursoInactivo = new Curso(_institucionId, "Curso Inactivo", "Area");
        cursoInactivo.AsignarId(_cursoId);
        cursoInactivo.Desactivar();
        await _cursoRepository.AddAsync(cursoInactivo);

        _tenantContext.Rol = "Administrador";

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _service.ValidarPermisoGestionAsync(_cursoId)
        );
    }

    [Fact]
    public async Task ValidarPermisoGestion_CuandoEsAdministrador_PermiteAcceso()
    {
        var curso = new Curso(_institucionId, "Curso Activo", "Area");
        curso.AsignarId(_cursoId);
        await _cursoRepository.AddAsync(curso);

        _tenantContext.Rol = "Administrador";
        _tenantContext.UsuarioId = Guid.NewGuid();

        await _service.ValidarPermisoGestionAsync(_cursoId);
    }

    [Fact]
    public async Task ValidarPermisoGestion_CuandoEsAlumno_LanzaForbiddenException()
    {
        // RN-MAT-02: El alumno no puede gestionar ni ver material (403)
        var curso = new Curso(_institucionId, "Curso Activo", "Area");
        curso.AsignarId(_cursoId);
        await _cursoRepository.AddAsync(curso);

        _tenantContext.Rol = "Alumno";
        _tenantContext.UsuarioId = Guid.NewGuid();

        var ex = await Assert.ThrowsAsync<ForbiddenException>(() =>
            _service.ValidarPermisoGestionAsync(_cursoId)
        );

        Assert.Contains("No tenés permiso", ex.Message);
    }

    [Fact]
    public async Task ValidarPermisoGestion_CuandoDocenteTieneCohorteActiva_PermiteAcceso()
    {
        // RN-MAT-01: Docente con cohorte activa en el curso
        var curso = new Curso(_institucionId, "Curso Activo", "Area");
        curso.AsignarId(_cursoId);
        await _cursoRepository.AddAsync(curso);

        var cohorte = new Cohorte(_institucionId, _cursoId, _docenteId, "Comision 1", "2026-1C");
        await _cohorteRepository.AddAsync(cohorte);

        _tenantContext.Rol = "Docente";
        _tenantContext.UsuarioId = _docenteId;

        await _service.ValidarPermisoGestionAsync(_cursoId);
    }

    [Fact]
    public async Task ValidarPermisoGestion_CuandoDocenteNoTieneCohorteEnCurso_LanzaForbiddenException()
    {
        // RN-MAT-01: Docente que no dicta en este curso (403)
        var curso = new Curso(_institucionId, "Curso Activo", "Area");
        curso.AsignarId(_cursoId);
        await _cursoRepository.AddAsync(curso);

        _tenantContext.Rol = "Docente";
        _tenantContext.UsuarioId = _docenteId;

        var ex = await Assert.ThrowsAsync<ForbiddenException>(() =>
            _service.ValidarPermisoGestionAsync(_cursoId)
        );

        Assert.Contains("No tenés permiso", ex.Message);
    }

    [Fact]
    public void ValidarAccesoTextoInterno_CuandoEsAlumno_LanzaForbiddenException()
    {
        // RN-MAT-02: Alumno no accede al endpoint interno
        _tenantContext.Rol = "Alumno";

        Assert.Throws<ForbiddenException>(() => _service.ValidarAccesoTextoInterno());
    }

    [Fact]
    public void ValidarAccesoTextoInterno_CuandoNoEsAlumno_PermiteAcceso()
    {
        _tenantContext.Rol = "Docente";

        _service.ValidarAccesoTextoInterno();
    }

    [Fact]
    public async Task ValidarPermisoLectura_CuandoCursoInactivoYEsAdministrador_PermiteAcceso()
    {
        // Consulta historica: El administrador puede leer apuntes aunque el curso este inactivo
        var cursoInactivo = new Curso(_institucionId, "Curso Inactivo", "Area");
        cursoInactivo.AsignarId(_cursoId);
        cursoInactivo.Desactivar();
        await _cursoRepository.AddAsync(cursoInactivo);

        _tenantContext.Rol = "Administrador";

        await _service.ValidarPermisoLecturaAsync(_cursoId);
    }

    [Fact]
    public async Task ValidarPermisoLectura_CuandoCursoNoExiste_LanzaKeyNotFoundException()
    {
        _tenantContext.Rol = "Administrador";

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _service.ValidarPermisoLecturaAsync(_cursoId)
        );
    }

    [Fact]
    public async Task ValidarPermisoLectura_CuandoEsAlumno_LanzaForbiddenException()
    {
        var curso = new Curso(_institucionId, "Curso Activo", "Area");
        curso.AsignarId(_cursoId);
        await _cursoRepository.AddAsync(curso);

        _tenantContext.Rol = "Alumno";

        var ex = await Assert.ThrowsAsync<ForbiddenException>(() =>
            _service.ValidarPermisoLecturaAsync(_cursoId)
        );
        Assert.Contains("No tenés permiso", ex.Message);
    }

    [Fact]
    public async Task ValidarPermisoLectura_CuandoDocenteTieneCohorteEnCursoInactivo_PermiteAcceso()
    {
        var cursoInactivo = new Curso(_institucionId, "Curso Inactivo", "Area");
        cursoInactivo.AsignarId(_cursoId);
        cursoInactivo.Desactivar();
        await _cursoRepository.AddAsync(cursoInactivo);

        var cohorte = new Cohorte(_institucionId, _cursoId, _docenteId, "Comision 1A", "2026-1C");
        await _cohorteRepository.AddAsync(cohorte);

        _tenantContext.Rol = "Docente";
        _tenantContext.UsuarioId = _docenteId;

        await _service.ValidarPermisoLecturaAsync(_cursoId);
    }
}
