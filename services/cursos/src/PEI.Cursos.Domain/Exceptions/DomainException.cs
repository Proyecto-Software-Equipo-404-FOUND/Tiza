using System;

namespace PEI.Cursos.Domain.Exceptions;

/*
 * Excepción base del Dominio para señalar violaciones a las reglas de negocio e invariantes.
 * En la capa API, el ProblemDetailsMiddleware captura esta excepción y la transforma en una
 * respuesta HTTP 400 Bad Request estructurada bajo el estándar RFC 9457.
 */
public class DomainException : Exception
{
    public DomainException(string message) : base(message)
    {
    }

    public DomainException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
