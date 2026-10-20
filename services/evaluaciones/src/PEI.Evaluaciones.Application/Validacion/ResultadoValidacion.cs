namespace PEI.Evaluaciones.Application.Validacion
{
    public record ResultadoValidacion(bool EsValido, IReadOnlyList<string> Errores)
    {
        public static ResultadoValidacion Valido() => new(true, Array.Empty<string>());
        public static ResultadoValidacion Invalido(IReadOnlyList<string> errores) => new(false, errores);
    }
}