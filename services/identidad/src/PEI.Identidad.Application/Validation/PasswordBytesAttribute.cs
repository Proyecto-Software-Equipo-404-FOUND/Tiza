using System.ComponentModel.DataAnnotations;
using System.Text;

namespace PEI.Identidad.Application.Validation;

public sealed class PasswordBytesAttribute : ValidationAttribute
{
    public override bool IsValid(object? value) => value is string password && Encoding.UTF8.GetByteCount(password) <= 72;
}
