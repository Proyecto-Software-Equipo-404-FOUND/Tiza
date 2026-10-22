using System;

namespace PEI.Cursos.Application.UseCases.Cohortes.Queries;

/* Consulta inmutable de validacion de existencia y pertenencia de una cohorte.
Encapsula el identificador de la comision recibido en la peticion inter-servicio. */
public record ValidarCohorteQuery(Guid CohorteId);
