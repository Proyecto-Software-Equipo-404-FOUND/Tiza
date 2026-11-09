using System;

namespace PEI.BuildingBlocks.Errors;

/* Excepcion lanzada cuando el archivo o payload enviado no coincide con el tipo de medio esperado.
ProblemDetailsMiddleware la captura y transforma en una respuesta HTTP 415 Unsupported Media Type (RFC 9457). */
public class UnsupportedMediaTypeException : Exception
{
    public UnsupportedMediaTypeException(string message) : base(message)
    {
    }

    public UnsupportedMediaTypeException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
