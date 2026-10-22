namespace PEI.Identidad.Application.UseCases.Auth.Refresh;

public class LogoutCommand
{
    public string RefreshToken { get; set; } = string.Empty;
}