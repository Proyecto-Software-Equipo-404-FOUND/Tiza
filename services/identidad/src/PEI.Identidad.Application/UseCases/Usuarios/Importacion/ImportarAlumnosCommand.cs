namespace PEI.Identidad.Application.UseCases.Usuarios.Importacion;

public sealed record FilaAlumnoCsv(int Numero, string Nombre, string Apellido, string Email);
public sealed record ErrorImportacionAlumno(int Fila, string Motivo);
public sealed record ResultadoImportacionAlumnos(int Creados, int Omitidos, IReadOnlyList<ErrorImportacionAlumno> Errores);
