using PEI.Identidad.Domain.Entities;
namespace PEI.Identidad.Application.Interfaces.Service;
public interface ITokenService { string GenerateToken(Usuario usuario); }
