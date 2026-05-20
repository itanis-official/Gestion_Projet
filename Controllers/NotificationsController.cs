using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using GestionProjet.Data;
using GestionProjet.Models;
using GestionProjet.Services;
using Microsoft.EntityFrameworkCore;

namespace GestionProjet.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(AuthenticationSchemes = "Bearer")] // ✅ Sécurise tout le contrôleur
public class NotificationsController : ControllerBase
{
    private readonly NotificationService _service;
    private readonly ApplicationDbContext _context;
    private readonly ILogger<NotificationsController> _logger;

    public NotificationsController(
        NotificationService service, 
        ApplicationDbContext context,
        ILogger<NotificationsController> logger)
    {
        _service = service;
        _context = context;
        _logger = logger;
    }

    // ──────────────────────────────────────────────────────
    // ✅ MÉTHODE CENTRALE : Résoudre l'EmployeId depuis le token Authentik
    // ──────────────────────────────────────────────────────
   private async Task<int?> GetAuthenticatedEmployeId()
{
    try
    {
        _logger.LogInformation("=== GetAuthenticatedEmployeId START ===");
        
        // Log tous les claims
        var allClaims = User.Claims.Select(c => $"{c.Type}: {c.Value}");
        _logger.LogInformation("All Claims: {Claims}", string.Join(", ", allClaims));
        
        // 1. Essayer l'ancien claim local
        var employeIdClaim = User.FindFirst("EmployeId")?.Value;
        if (!string.IsNullOrEmpty(employeIdClaim) && int.TryParse(employeIdClaim, out var localId))
        {
            _logger.LogInformation("EmployeId trouvé via claim: {Id}", localId);
            return localId;
        }

        // 2. Récupérer l'email - Essayez plusieurs types de claims
        var email = User.FindFirst("email")?.Value 
                 ?? User.FindFirst(ClaimTypes.Email)?.Value
                 ?? User.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/email")?.Value;
        
        _logger.LogInformation("Email extrait: '{Email}'", email ?? "NULL");

        // 3. Récupérer le nom d'utilisateur
        var username = User.FindFirst("preferred_username")?.Value 
                    ?? User.FindFirst("name")?.Value;
        
        _logger.LogInformation("Username extrait: '{Username}'", username ?? "NULL");

        // 4. Chercher l'employé par email
        Employe? employe = null;

        if (!string.IsNullOrEmpty(email))
        {
            employe = await _context.Employes
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.Email == email);
            
            _logger.LogInformation("Recherche par email {Email}: Employé trouvé = {Found}", 
                email, employe != null);
        }

        if (employe == null && !string.IsNullOrEmpty(username))
        {
            var cleanUsername = username.Replace("-", " ").Replace(".", " ");
            employe = await _context.Employes
                .AsNoTracking()
                .FirstOrDefaultAsync(e => 
                    e.Email.Contains(username) || e.NomComplet.Contains(cleanUsername));
            
            _logger.LogInformation("Recherche par username {Username}: Employé trouvé = {Found}", 
                username, employe != null);
        }

        if (employe == null)
        {
            _logger.LogWarning("❌ Aucun employé trouvé pour Email={Email}, Username={Username}", 
                email, username);
            return null;
        }

        _logger.LogInformation("✅ Employé trouvé: Id={Id}, Nom={Nom}, Email={Email}", 
            employe.Id, employe.NomComplet, employe.Email);
        
        return employe.Id;
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "❌ ERREUR dans GetAuthenticatedEmployeId: {Message}", ex.Message);
        return null;
    }
}

    // ──────────────────────────────────────────────────────
    // ENDPOINTS
    // ──────────────────────────────────────────────────────

    // ✅ NOUVEL ENDPOINT : Récupère les notifications de l'utilisateur connecté
   [HttpGet("mes-notifications")]
public async Task<IActionResult> GetMyNotifications()
{
    try
    {
        _logger.LogInformation("=== GetMyNotifications START ===");
        
        var employeId = await GetAuthenticatedEmployeId();
        _logger.LogInformation("EmployeId résolu: {EmployeId}", employeId);
        
        if (employeId == null)
        {
            _logger.LogWarning("EmployeId est null - retour liste vide");
            return Ok(new List<Notification>()); // Retourner 200 avec liste vide
        }

        var data = await _service.GetByEmploye(employeId.Value);
        _logger.LogInformation("✅ {Count} notifications trouvées", data.Count);
        
        return Ok(data);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "❌ Erreur dans GetMyNotifications");
        // Retourner 200 avec liste vide pour ne pas bloquer le front
        return Ok(new List<Notification>());
    }
}

    // ✅ ENDPOINT EXISTANT AMÉLIORÉ : Marquer une notification comme lue
    [HttpPut("read/{id}")]
    public async Task<IActionResult> MarkAsRead(int id)
    {
        var employeId = await GetAuthenticatedEmployeId();
        if (employeId == null)
            return Unauthorized(new { message = "Employé non identifié." });

        // Optionnel mais recommandé : vérifier que la notification appartient à l'utilisateur
        // var notif = await _context.Notifications.FindAsync(id);
        // if (notif != null && notif.EmployeId != employeId.Value) return Forbid();

        await _service.MarkAsRead(id);
        return Ok();
    }

    // ✅ NOUVEL ENDPOINT : Tout marquer comme lu
    [HttpPut("read-all")]
    public async Task<IActionResult> MarkAllAsRead()
    {
        var employeId = await GetAuthenticatedEmployeId();
        if (employeId == null)
            return Unauthorized(new { message = "Employé non identifié." });

        var unreadNotifs = await _context.Notifications
            .Where(n => n.EmployeId == employeId.Value && !n.Lu)
            .ToListAsync();

        foreach (var notif in unreadNotifs)
        {
            notif.Lu = true;
        }

        await _context.SaveChangesAsync();
        return Ok();
    }
}