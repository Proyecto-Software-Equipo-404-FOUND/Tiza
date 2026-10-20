using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace PEI.Identidad.Domain.Entities;

public class CuentaPlataforma
{
    public Guid Id { get; set; } = Guid.NewGuid();

    private string _email = string.Empty;

    public string Email
    {
        get => _email;
        set => _email = (value ?? throw new ArgumentNullException(nameof(value))).Trim();
    }

    public string PasswordHash { get; set; } = string.Empty;

    public string Nombre { get; set; } = string.Empty;

    public string Apellido { get; set; } = string.Empty;

    public string Rol { get; set; } = "AdministradorGlobal";

    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;

    public bool Activo { get; set; } = true;
}
