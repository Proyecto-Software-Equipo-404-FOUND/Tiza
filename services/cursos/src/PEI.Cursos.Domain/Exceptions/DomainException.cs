using System;

namespace PEI.Cursos.Domain.Exceptions;

/* Excepcion base del Dominio para senalar violaciones a las reglas de negocio e invariantes.
Hereda de ArgumentException para que el ProblemDetailsMiddleware de BuildingBlocks
la capture y transforme automaticamente en una respuesta HTTP 400 Bad Request (RFC 9457). */
public class DomainException : ArgumentException
{
    public DomainException(string message) : base(message)
    {
    }

    public DomainException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
