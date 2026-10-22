using System.ComponentModel.DataAnnotations;
namespace PEI.Identidad.Application.UseCases.Auth.Refresh;

public class RefreshCommand
{
    [MaxLength(256)] public string RefreshToken { get; set; } = string.Empty;
}
