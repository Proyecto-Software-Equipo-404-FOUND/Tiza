namespace PEI.Identidad.Infrastructure.Persistence;

/// <summary>La misma funcion PostgreSQL que calcula la clave unica global.</summary>
public static class EmailDbFunctions
{
    // Solo se utiliza en LINQ traducido. No aplicar ToLower de .NET al parametro:
    // su cultura puede diferir de la colacion de PostgreSQL.
    public static string Normalize(string email) =>
        throw new NotSupportedException("Esta funcion solo puede ejecutarse en PostgreSQL mediante EF Core.");
}
