using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using PEI.Cursos.Application.Common;
using PEI.Cursos.Application.Common.DTOs;
using PEI.Cursos.Application.DTOs.Cohortes;
using PEI.Cursos.Application.DTOs.Cursos;
using PEI.Cursos.Application.Interfaces;
using PEI.Cursos.Application.UseCases.Cohortes.Commands;
using PEI.Cursos.Application.UseCases.Cohortes.Queries;
using PEI.Cursos.Application.UseCases.Cursos.Commands;
using PEI.Cursos.Application.UseCases.Cursos.Queries;

namespace PEI.Cursos.Application;

/* Configuracion de inyeccion de dependencias para la capa de Application.
Registra el despachador Mediator y todos los handlers de Commands y Queries. */
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Registrar el despachador central de casos de uso
        services.AddScoped<IMediator, Mediator>();

        // Registrar Handlers de Cursos
        services.AddScoped<IRequestHandler<CrearCursoCommand, CursoDto>, CrearCursoCommandHandler>();
        services.AddScoped<IRequestHandler<ActualizarCursoCommand, CursoDto?>, ActualizarCursoCommandHandler>();
        services.AddScoped<IRequestHandler<DesactivarCursoCommand, bool>, DesactivarCursoCommandHandler>();
        services.AddScoped<IRequestHandler<GetCursosQuery, ResultadoPaginadoDTO<CursoDto>>, GetCursosQueryHandler>();
        services.AddScoped<IRequestHandler<GetCursoByIdQuery, CursoDto?>, GetCursoByIdQueryHandler>();

        // Registrar Handlers de Cohortes
        services.AddScoped<IRequestHandler<CrearCohorteCommand, CohorteDto>, CrearCohorteCommandHandler>();
        services.AddScoped<IRequestHandler<GetCohortesByCursoQuery, IReadOnlyList<CohorteDto>?>, GetCohortesByCursoQueryHandler>();
        services.AddScoped<IRequestHandler<ValidarCohorteQuery, CohorteValidacionDto?>, ValidarCohorteQueryHandler>();

        return services;
    }
}
