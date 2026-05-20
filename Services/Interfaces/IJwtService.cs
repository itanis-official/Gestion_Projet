using System.Security.Claims;
using GestionProjet.Models;

namespace GestionProjet.Services.Interfaces;

public interface IJwtService
{
    string GenerateJwtToken(Utilisateur user, IList<string> roles);
    string GenerateRefreshToken();
    ClaimsPrincipal? GetPrincipalFromExpiredToken(string token);
}