namespace PEI.BuildingBlocks.Errors;

public abstract class CodedConflictException(string code, string message) : InvalidOperationException(message)
{
    public string Code { get; } = code;

    /// <summary>Datos extra que viajan en el ProblemDetails (los null no se envían).</summary>
    public IDictionary<string, object?> Extensions { get; } = new Dictionary<string, object?>();
}
