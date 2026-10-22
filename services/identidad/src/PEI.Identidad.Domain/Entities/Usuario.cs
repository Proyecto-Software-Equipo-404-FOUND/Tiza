using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PEI.Identidad.Domain.Entities;

public class Usuario
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InstitucionId { get; set; }
    private string _email = string.Empty;
    public string Email
    {
        get => _email;
        set => _email = (value ?? throw new ArgumentNullException(nameof(value))).Trim();
    }
    public string PasswordHash { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string Apellido { get; set; } = string.Empty;
    public string Rol { get; set; } = string.Empty;
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
    public bool Activo { get; set; } = true;
    public Institucion Institucion { get; set; } = null!;
    public ICollection<RefreshToken> RefreshTokens {get; set;} = new List<RefreshToken>();
}
