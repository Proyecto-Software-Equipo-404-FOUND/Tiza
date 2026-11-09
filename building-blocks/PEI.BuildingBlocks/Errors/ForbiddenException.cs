using System;

namespace PEI.BuildingBlocks.Errors;

/* Excepcion de seguridad lanzada cuando un usuario autenticado carece de los permisos
necesarios para acceder o modificar un recurso especifico (RN-MAT-01 y RN-MAT-02).
ProblemDetailsMiddleware la captura y serializa como respuesta HTTP 403 Forbidden (RFC 9457). */
public class ForbiddenException : Exception
{
    public ForbiddenException(string message) : base(message)
    {
    }

    public ForbiddenException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
