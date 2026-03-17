using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using GestionProjet.Models;
using GestionProjet.DTOs.Auth;
using GestionProjet.Services.Interfaces;
using GestionProjet.Data;

namespace GestionProjet.Controllers;

[Route("api/[controller]")]
[ApiController]
public class AuthController : ControllerBase
{
    private readonly UserManager<Utilisateur> _userManager;
    private readonly SignInManager<Utilisateur> _signInManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly IJwtService _jwtService;
    private readonly ApplicationDbContext _context;
    private readonly IConfiguration _configuration;

    public AuthController(
        UserManager<Utilisateur> userManager,
        SignInManager<Utilisateur> signInManager,
        RoleManager<IdentityRole> roleManager,
        IJwtService jwtService,
        ApplicationDbContext context,
        IConfiguration configuration)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _roleManager = roleManager;
        _jwtService = jwtService;
        _context = context;
        _configuration = configuration;
    }

    [HttpPost("register")]
    public async Task<ActionResult<AuthResponseDto>> Register(RegisterDto registerDto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

      
        var existingUser = await _userManager.FindByEmailAsync(registerDto.Email);
        if (existingUser != null)
        {
            return BadRequest(new AuthResponseDto 
            { 
                Success = false, 
                Message = "Un utilisateur avec cet email existe déjà." 
            });
        }

       
        var user = new Utilisateur
        {
            UserName = registerDto.Email,
            Email = registerDto.Email,
            NomComplet = registerDto.NomComplet,
            Poste = registerDto.Poste,
            Departement = registerDto.Departement,
            DateCreation = DateTime.UtcNow,
            EstActif = true
        };

        var result = await _userManager.CreateAsync(user, registerDto.Password);

        if (!result.Succeeded)
        {
            return BadRequest(new AuthResponseDto
            {
                Success = false,
                Message = "Erreur lors de la création de l'utilisateur",
                Errors = result.Errors.Select(e => e.Description).ToList()
            });
        }


        await _userManager.AddToRoleAsync(user, "User");

        var roles = await _userManager.GetRolesAsync(user);
        var token = _jwtService.GenerateJwtToken(user, roles);
        var refreshToken = _jwtService.GenerateRefreshToken();

        await SaveRefreshToken(user.Id, refreshToken);

        return Ok(new AuthResponseDto
        {
            Success = true,
            Message = "Inscription réussie",
            Token = token,
            RefreshToken = refreshToken,
            Expiration = DateTime.UtcNow.AddMinutes(_configuration.GetValue<int>("JwtSettings:ExpirationInMinutes")),
            UserId = user.Id,
            Email = user.Email,
            NomComplet = user.NomComplet,
            Roles = roles.ToList()
        });
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponseDto>> Login(LoginDto loginDto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var user = await _userManager.FindByEmailAsync(loginDto.Email);
        if (user == null)
        {
            return Unauthorized(new AuthResponseDto 
            { 
                Success = false, 
                Message = "Email ou mot de passe incorrect." 
            });
        }

        if (!user.EstActif)
        {
            return Unauthorized(new AuthResponseDto 
            { 
                Success = false, 
                Message = "Ce compte est désactivé. Contactez l'administrateur." 
            });
        }

        var result = await _signInManager.CheckPasswordSignInAsync(user, loginDto.Password, false);

        if (!result.Succeeded)
        {
            return Unauthorized(new AuthResponseDto 
            { 
                Success = false, 
                Message = "Email ou mot de passe incorrect." 
            });
        }

        await _userManager.UpdateAsync(user);

        var roles = await _userManager.GetRolesAsync(user);
        var token = _jwtService.GenerateJwtToken(user, roles);
        var refreshToken = _jwtService.GenerateRefreshToken();

        await RevokeOldRefreshTokens(user.Id);
        await SaveRefreshToken(user.Id, refreshToken);

        return Ok(new AuthResponseDto
        {
            Success = true,
            Message = "Connexion réussie",
            Token = token,
            RefreshToken = refreshToken,
            Expiration = DateTime.UtcNow.AddMinutes(_configuration.GetValue<int>("JwtSettings:ExpirationInMinutes")),
            UserId = user.Id,
            Email = user.Email,
            NomComplet = user.NomComplet,
            Roles = roles.ToList()
        });
    }

    [HttpPost("refresh-token")]
    public async Task<ActionResult<AuthResponseDto>> RefreshToken(RefreshTokenDto refreshTokenDto)
    {
        if (string.IsNullOrEmpty(refreshTokenDto.Token) || string.IsNullOrEmpty(refreshTokenDto.RefreshToken))
        {
            return BadRequest(new AuthResponseDto 
            { 
                Success = false, 
                Message = "Token et refresh token sont requis." 
            });
        }

        try
        {
            var principal = _jwtService.GetPrincipalFromExpiredToken(refreshTokenDto.Token);
            var userId = principal?.FindFirst("userId")?.Value;

            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new AuthResponseDto 
                { 
                    Success = false, 
                    Message = "Token invalide." 
                });
            }

            var refreshToken = await _context.RefreshTokens
                .FirstOrDefaultAsync(rt => rt.Token == refreshTokenDto.RefreshToken 
                    && rt.UserId == userId 
                    && !rt.IsRevoked 
                    && rt.ExpiryDate > DateTime.UtcNow);

            if (refreshToken == null)
            {
                return Unauthorized(new AuthResponseDto 
                { 
                    Success = false, 
                    Message = "Refresh token invalide ou expiré." 
                });
            }

            refreshToken.IsRevoked = true;
            await _context.SaveChangesAsync();

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return Unauthorized(new AuthResponseDto 
                { 
                    Success = false, 
                    Message = "Utilisateur non trouvé." 
                });
            }

            var roles = await _userManager.GetRolesAsync(user);
            var newToken = _jwtService.GenerateJwtToken(user, roles);
            var newRefreshToken = _jwtService.GenerateRefreshToken();

            await SaveRefreshToken(user.Id, newRefreshToken);

            return Ok(new AuthResponseDto
            {
                Success = true,
                Message = "Token rafraîchi avec succès",
                Token = newToken,
                RefreshToken = newRefreshToken,
                Expiration = DateTime.UtcNow.AddMinutes(_configuration.GetValue<int>("JwtSettings:ExpirationInMinutes")),
                UserId = user.Id,
                Email = user.Email,
                NomComplet = user.NomComplet,
                Roles = roles.ToList()
            });
        }
        catch (Exception ex)
        {
            return Unauthorized(new AuthResponseDto 
            { 
                Success = false, 
                Message = "Token invalide.", 
                Errors = new List<string> { ex.Message }
            });
        }
    }

    [HttpPost("logout")]
    [Microsoft.AspNetCore.Authorization.Authorize]
    public async Task<IActionResult> Logout()
    {
        var userId = User.FindFirst("userId")?.Value;
        if (!string.IsNullOrEmpty(userId))
        {
            await RevokeAllUserRefreshTokens(userId);
        }

        return Ok(new { message = "Déconnexion réussie" });
    }

   
    [HttpGet("me")]
    [Microsoft.AspNetCore.Authorization.Authorize]
    public async Task<ActionResult<object>> GetCurrentUser()
    {
        var userId = User.FindFirst("userId")?.Value;
        var user = await _userManager.FindByIdAsync(userId ?? string.Empty);

        if (user == null)
        {
            return NotFound();
        }

        var roles = await _userManager.GetRolesAsync(user);

        return Ok(new
        {
            user.Id,
            user.Email,
            user.NomComplet,
            user.Poste,
            user.Departement,
            user.EstActif,
            user.DateCreation,
            Roles = roles
        });
    }

    private async Task SaveRefreshToken(string userId, string token)
    {
        var refreshToken = new RefreshToken
        {
            Token = token,
            UserId = userId,
            ExpiryDate = DateTime.UtcNow.AddDays(
                _configuration.GetValue<int>("JwtSettings:RefreshTokenExpirationInDays")),
            IsRevoked = false,
            CreatedAt = DateTime.UtcNow
        };

        _context.RefreshTokens.Add(refreshToken);
        await _context.SaveChangesAsync();
    }

    private async Task RevokeOldRefreshTokens(string userId)
    {
        var oldTokens = await _context.RefreshTokens
            .Where(rt => rt.UserId == userId && !rt.IsRevoked)
            .ToListAsync();

        foreach (var token in oldTokens)
        {
            token.IsRevoked = true;
        }

        await _context.SaveChangesAsync();
    }

    private async Task RevokeAllUserRefreshTokens(string userId)
    {
        var tokens = await _context.RefreshTokens
            .Where(rt => rt.UserId == userId && !rt.IsRevoked)
            .ToListAsync();

        foreach (var token in tokens)
        {
            token.IsRevoked = true;
        }

        await _context.SaveChangesAsync();
    }
}