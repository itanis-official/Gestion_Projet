using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
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
    private readonly IJwtService _jwtService;
    private readonly ApplicationDbContext _context;
    private readonly IConfiguration _configuration;

    public AuthController(
        UserManager<Utilisateur> userManager,
        SignInManager<Utilisateur> signInManager,
        IJwtService jwtService,
        ApplicationDbContext context,
        IConfiguration configuration)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _jwtService = jwtService;
        _context = context;
        _configuration = configuration;
    }

    // ========================= REGISTER =========================
    [HttpPost("register")]
    public async Task<ActionResult<AuthResponseDto>> Register(RegisterDto registerDto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var existingUser = await _userManager.FindByEmailAsync(registerDto.Email);
        if (existingUser != null)
        {
            return BadRequest(new AuthResponseDto
            {
                Success = false,
                Message = "Email déjà utilisé"
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
                Errors = result.Errors.Select(e => e.Description).ToList()
            });
        }

        // ================= CREATE / LINK EMPLOYE =================
        var employe = await _context.Employes.FirstOrDefaultAsync(e => e.Email == user.Email);

        if (employe == null)
        {
            employe = new Employe
            {
                NomComplet = user.NomComplet,
                Email = user.Email,
                Role = "Employe"
            };

            _context.Employes.Add(employe);
            await _context.SaveChangesAsync();
        }

        // LINK
        user.EmployeId = employe.Id;
        await _userManager.UpdateAsync(user);

        // 🔥 IMPORTANT: reload user to ensure EF + Identity sync
        user = await _userManager.FindByIdAsync(user.Id);

        await _userManager.AddToRoleAsync(user, "Employe");

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
            Expiration = DateTime.UtcNow.AddMinutes(
                _configuration.GetValue<int>("JwtSettings:ExpirationInMinutes")
            ),
            UserId = user.Id,
            Email = user.Email,
            NomComplet = user.NomComplet,
            Roles = roles.ToList()
        });
    }

    // ========================= LOGIN =========================
    [HttpPost("login")]
    public async Task<ActionResult<AuthResponseDto>> Login(LoginDto loginDto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var user = await _userManager.FindByEmailAsync(loginDto.Email);

        if (user == null || !user.EstActif)
        {
            return Unauthorized(new AuthResponseDto
            {
                Success = false,
                Message = "Email ou mot de passe incorrect"
            });
        }

        var result = await _signInManager.CheckPasswordSignInAsync(user, loginDto.Password, false);

        if (!result.Succeeded)
        {
            return Unauthorized(new AuthResponseDto
            {
                Success = false,
                Message = "Email ou mot de passe incorrect"
            });
        }

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
            Expiration = DateTime.UtcNow.AddMinutes(
                _configuration.GetValue<int>("JwtSettings:ExpirationInMinutes")
            ),
            UserId = user.Id,
            Email = user.Email,
            NomComplet = user.NomComplet,
            Roles = roles.ToList()
        });
    }

    // ========================= ME =========================
    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<object>> GetCurrentUser()
    {
        var userId = User.FindFirst("userId")?.Value;

        var user = await _userManager.FindByIdAsync(userId!);
        if (user == null) return NotFound();

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
            user.EmployeId,
            Roles = roles
        });
    }

    // ========================= REFRESH TOKEN =========================
    [HttpPost("refresh-token")]
    public async Task<ActionResult<AuthResponseDto>> RefreshToken(RefreshTokenDto dto)
    {
        var principal = _jwtService.GetPrincipalFromExpiredToken(dto.Token);
        var userId = principal?.FindFirst("userId")?.Value;

        if (userId == null)
            return Unauthorized();

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
            return Unauthorized();

        var roles = await _userManager.GetRolesAsync(user);

        var newToken = _jwtService.GenerateJwtToken(user, roles);
        var newRefreshToken = _jwtService.GenerateRefreshToken();

        await SaveRefreshToken(user.Id, newRefreshToken);

        return Ok(new AuthResponseDto
        {
            Success = true,
            Token = newToken,
            RefreshToken = newRefreshToken,
            UserId = user.Id,
            Email = user.Email,
            NomComplet = user.NomComplet,
            Roles = roles.ToList()
        });
    }

    // ========================= HELPERS =========================
    private async Task SaveRefreshToken(string userId, string token)
    {
        _context.RefreshTokens.Add(new RefreshToken
        {
            Token = token,
            UserId = userId,
            ExpiryDate = DateTime.UtcNow.AddDays(
                _configuration.GetValue<int>("JwtSettings:RefreshTokenExpirationInDays")
            )
        });

        await _context.SaveChangesAsync();
    }

    private async Task RevokeOldRefreshTokens(string userId)
    {
        var tokens = await _context.RefreshTokens
            .Where(x => x.UserId == userId && !x.IsRevoked)
            .ToListAsync();

        foreach (var t in tokens)
            t.IsRevoked = true;

        await _context.SaveChangesAsync();
    }
}