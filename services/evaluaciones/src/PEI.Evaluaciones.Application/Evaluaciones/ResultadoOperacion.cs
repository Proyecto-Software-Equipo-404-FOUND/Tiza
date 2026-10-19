namespace PEI.Evaluaciones.Application.Evaluaciones;

public enum TipoResultado
{
    Ok,
    Invalido,
    NoEncontrado
}

public class ResultadoOperacion<T>
{
    private ResultadoOperacion(TipoResultado tipo, T? valor, IReadOnlyList<string> errores)
    {
        Tipo = tipo;
        Valor = valor;
        Errores = errores;
    }

    public TipoResultado Tipo { get; }

    public T? Valor { get; }

    public IReadOnlyList<string> Errores { get; }

    public static ResultadoOperacion<T> Ok(T valor) =>
        new(TipoResultado.Ok, valor, Array.Empty<string>());

    public static ResultadoOperacion<T> Invalido(IReadOnlyList<string> errores) =>
        new(TipoResultado.Invalido, default, errores);

    public static ResultadoOperacion<T> Invalido(string error) =>
        new(TipoResultado.Invalido, default, new[] { error });

    public static ResultadoOperacion<T> NoEncontrado() =>
        new(TipoResultado.NoEncontrado, default, Array.Empty<string>());
}