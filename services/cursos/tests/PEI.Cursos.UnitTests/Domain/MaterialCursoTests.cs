using System;
using PEI.Cursos.Domain.Entities;
using PEI.Cursos.Domain.Enums;
using PEI.Cursos.Domain.Exceptions;
using Xunit;

namespace PEI.Cursos.UnitTests.Domain;

/* Pruebas unitarias para la entidad de dominio MaterialCurso.
Verifica las reglas de negocio e invariantes (RN-MAT-03 a RN-MAT-05). */
public class MaterialCursoTests
{
    private readonly Guid _institucionId = Guid.NewGuid();
    private readonly Guid _cursoId = Guid.NewGuid();
    private readonly Guid _subidoPorId = Guid.NewGuid();

    [Fact]
    public void Constructor_ConDatosValidos_InstanciaMaterialEnEstadoListo()
    {
        var material = new MaterialCurso(
            _institucionId,
            _cursoId,
            "Unidad 1 - Arquitectura",
            "unidad1.pdf",
            1024 * 100,
            10,
            "storage/unidad1.pdf",
            "Contenido con texto seleccionable.",
            _subidoPorId
        );

        Assert.Equal(_institucionId, material.InstitucionId);
        Assert.Equal(_cursoId, material.CursoId);
        Assert.Equal("Unidad 1 - Arquitectura", material.Titulo);
        Assert.Equal("unidad1.pdf", material.NombreArchivo);
        Assert.Equal(1024 * 100, material.TamanoBytes);
        Assert.Equal(10, material.CantidadPaginas);
        Assert.Equal("storage/unidad1.pdf", material.UbicacionArchivo);
        Assert.Equal("Contenido con texto seleccionable.", material.TextoExtraido);
        Assert.Equal(EstadoMaterialCurso.Listo, material.Estado);
        Assert.True(material.Activo);
        Assert.Equal(1, material.Version);
    }

    [Fact]
    public void Constructor_CuandoTamanoSupera10MB_LanzaDomainException()
    {
        // RN-MAT-03: Limite maximo de 10 MB
        long tamanoExcedido = (10 * 1024 * 1024) + 1;

        var ex = Assert.Throws<DomainException>(() => new MaterialCurso(
            _institucionId,
            _cursoId,
            "Titulo Valido",
            "archivo.pdf",
            tamanoExcedido,
            5,
            "path/archivo.pdf",
            "Texto valido",
            _subidoPorId
        ));

        Assert.Contains("10 MB", ex.Message);
    }

    [Fact]
    public void Constructor_CuandoPaginasSuperan30_LanzaDomainException()
    {
        // RN-MAT-03: Limite maximo de 30 paginas
        var ex = Assert.Throws<DomainException>(() => new MaterialCurso(
            _institucionId,
            _cursoId,
            "Titulo Valido",
            "archivo.pdf",
            1024,
            31,
            "path/archivo.pdf",
            "Texto valido",
            _subidoPorId
        ));

        Assert.Contains("30 paginas", ex.Message);
    }

    [Fact]
    public void Constructor_CuandoTextoExtraidoEstaVacio_LanzaDomainException()
    {
        // RN-MAT-04: Texto seleccionable obligatorio sin OCR
        var ex = Assert.Throws<DomainException>(() => new MaterialCurso(
            _institucionId,
            _cursoId,
            "Titulo Valido",
            "archivo.pdf",
            1024,
            5,
            "path/archivo.pdf",
            "   ",
            _subidoPorId
        ));

        Assert.Contains("no tiene texto seleccionable", ex.Message);
    }

    [Fact]
    public void Constructor_CuandoTituloSupera150Caracteres_LanzaDomainException()
    {
        var tituloLargo = new string('A', 151);

        var ex = Assert.Throws<DomainException>(() => new MaterialCurso(
            _institucionId,
            _cursoId,
            tituloLargo,
            "archivo.pdf",
            1024,
            5,
            "path/archivo.pdf",
            "Texto valido",
            _subidoPorId
        ));

        Assert.Contains("150 caracteres", ex.Message);
    }

    [Fact]
    public void Desactivar_CuandoEstaActivo_EjecutaBajaLogicaPreservandoDatos()
    {
        // RN-MAT-05: Borrar no afecta a evaluaciones pasadas
        var material = new MaterialCurso(
            _institucionId,
            _cursoId,
            "Titulo",
            "archivo.pdf",
            1024,
            5,
            "path/archivo.pdf",
            "Texto valido",
            _subidoPorId
        );

        material.Desactivar();

        Assert.False(material.Activo);
        Assert.NotNull(material.FechaModificacionUtc);
    }

    [Fact]
    public void Desactivar_CuandoYaEstaInactivo_LanzaDomainException()
    {
        var material = new MaterialCurso(
            _institucionId,
            _cursoId,
            "Titulo",
            "archivo.pdf",
            1024,
            5,
            "path/archivo.pdf",
            "Texto valido",
            _subidoPorId
        );

        material.Desactivar();

        var ex = Assert.Throws<DomainException>(() => material.Desactivar());
        Assert.Contains("inactivo", ex.Message);
    }
}
