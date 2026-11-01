namespace PEI.SalaEnVivo.Application.DTOs.Salas
{
    public sealed record ResultadoControlSala<T>(
        bool Exito,
        T? Valor,
        string? Codigo,
        string? Mensaje)
    {
        public static ResultadoControlSala<T> Ok(T valor) =>
            new(true, valor, null, null);

        public static ResultadoControlSala<T> Error(string codigo, string mensaje) =>
            new(false, default, codigo, mensaje);
    }
}
