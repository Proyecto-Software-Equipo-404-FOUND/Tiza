namespace PEI.BuildingBlocks.Errors;

public abstract class CodedConflictException(string code, string message) : InvalidOperationException(message)
{
    public string Code { get; } = code;
}
