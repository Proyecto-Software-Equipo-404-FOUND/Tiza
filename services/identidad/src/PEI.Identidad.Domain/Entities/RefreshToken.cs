using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PEI.Identidad.Domain.Entities;

public class RefreshToken
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UsuarioId { get; set; }
    public string Token { get; set; } = string.Empty;
    public DateTime FechaExpiracion { get; set; }
    public bool Revocado { get; set; } = false;
    public Usuario Usuario { get; set; } = null!;
}
