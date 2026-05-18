using GestionProjet.Data;
using GestionProjet.Models;
using Microsoft.EntityFrameworkCore;

namespace GestionProjet.Services;

public class NotificationService
{
    private readonly ApplicationDbContext _context;

    public NotificationService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task NotifierAssignationResponsable(int tacheId, int responsableId, string projetNom, string tacheTitre)
    {
        try
        {
            var tache = await _context.Taches
                .Include(t => t.Phase)
                    .ThenInclude(p => p.Projet)
                .FirstOrDefaultAsync(t => t.Id == tacheId);
            
            var projetId = tache?.Phase?.Projet?.Id;
            
            var message = $"📋 Vous avez été assigné comme RESPONSABLE de la tâche \"{tacheTitre}\" dans le projet \"{projetNom}\".";

            var notification = new Notification
            {
                EmployeId = responsableId,
                ProjetId = projetId,
                Message = message,
                Type = "AssignationResponsable",
                DateEnvoi = DateTime.Now,
                Lu = false
            };
            
            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync();
            
            Console.WriteLine($"✅ Notification envoyée à l'employé {responsableId}: {message}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erreur lors de l'envoi de la notification: {ex.Message}");
        }
    }

    public async Task NotifierAssignationTesteur(int tacheId, int testeurId, string projetNom, string tacheTitre)
    {
        try
        {
            var tache = await _context.Taches
                .Include(t => t.Phase)
                    .ThenInclude(p => p.Projet)
                .FirstOrDefaultAsync(t => t.Id == tacheId);
            
            var projetId = tache?.Phase?.Projet?.Id;
            
            var message = $"🧪 Vous avez été assigné comme TESTEUR de la tâche \"{tacheTitre}\" dans le projet \"{projetNom}\".";

            var notification = new Notification
            {
                EmployeId = testeurId,
                ProjetId = projetId,
                Message = message,
                Type = "AssignationTesteur",
                DateEnvoi = DateTime.Now,
                Lu = false
            };
            
            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync();
            
            Console.WriteLine($"✅ Notification envoyée à l'employé {testeurId}: {message}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erreur lors de l'envoi de la notification: {ex.Message}");
        }
    }
public async Task NotifierSousTacheATester(int sousTacheId, int testeurId, string tacheTitre, string sousTacheTitre)
{
    try
    {
        var message = $"🧪 La sous-tâche \"{sousTacheTitre}\" de la tâche \"{tacheTitre}\" est prête pour le TEST.";

        var notification = new Notification
        {
            EmployeId = testeurId,
            Message = message,
            Type = "SousTacheATester", 
            DateEnvoi = DateTime.Now,
            Lu = false
        };
        
        _context.Notifications.Add(notification);
        await _context.SaveChangesAsync();
        
        Console.WriteLine($"✅ Notification 'À tester' envoyée au testeur {testeurId}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Erreur lors de l'envoi de la notification 'À tester': {ex.Message}");
    }
}
   
    public async Task NotifierSousTacheValidee(int sousTacheId, int responsableId, string tacheTitre, string sousTacheTitre)
    {
        try
        {
            var message = $"✅ La sous-tâche \"{sousTacheTitre}\" de la tâche \"{tacheTitre}\" a été VALIDÉE avec succès.";

            var notification = new Notification
            {
                EmployeId = responsableId,
                Message = message,
                Type = "SousTacheValidee",
                DateEnvoi = DateTime.Now,
                Lu = false
            };
            
            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync();
            
            Console.WriteLine($"✅ Notification de validation envoyée à l'employé {responsableId}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erreur lors de l'envoi de la notification: {ex.Message}");
        }
    }

    public async Task NotifierSousTacheRejetee(int sousTacheId, int responsableId, string tacheTitre, string sousTacheTitre, string raison)
    {
        try
        {
            var message = $"❌ La sous-tâche \"{sousTacheTitre}\" de la tâche \"{tacheTitre}\" a été REJETÉE.\nRaison : {raison}";

            var notification = new Notification
            {
                EmployeId = responsableId,
                Message = message,
                Type = "SousTacheRejetee",
                DateEnvoi = DateTime.Now,
                Lu = false
            };
            
            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync();
            
            Console.WriteLine($"✅ Notification de rejet envoyée à l'employé {responsableId}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erreur lors de l'envoi de la notification: {ex.Message}");
        }
    }
       public async Task NotifierProjetsProchesFin()
    {
        var now = DateTime.Now;

        // 1. Calcul des bornes (Optimisé)
        var debut = now.Date;
        var fin = now.Date.AddDays(11);

        // 2. Récupération des projets (Optimisé)
        var projets = await _context.Projets
            .Include(p => p.GroupeEquipe)
                .ThenInclude(g => g.Employes)
            .Where(p => p.DateFinPrevue >= debut &&
                        p.DateFinPrevue < fin)
            .ToListAsync();

        // 3. Récupération des notifications existantes
        // ✅ CORRECTION ICI : On utilise un "Anonymous Type" dans le Select pour SQL
        var notificationsData = await _context.Notifications
            .Where(n => n.Type == "ProjetFinProche" && n.DateEnvoi >= debut)
            .Select(n => new { n.EmployeId, n.ProjetId }) 
            .ToListAsync();

        // 4. Conversion en HashSet de tuples (en mémoire, pas en SQL)
        // On filtre les nulls et on crée les tuples ici
        var clesNotifs = new HashSet<(int EmpId, int ProjId)>(
            notificationsData
                .Where(x => x.ProjetId.HasValue) 
                .Select(x => (x.EmployeId, x.ProjetId.Value)) 
        );

        var nouvellesNotifications = new List<Notification>();

        foreach (var projet in projets)
        {
            if (projet.GroupeEquipe == null) continue;

            foreach (var employe in projet.GroupeEquipe.Employes)
            {
                var message = $"⚠️ Le projet '{projet.Nom}' se termine le {projet.DateFinPrevue:dd/MM/yyyy}";
                
                // Vérification en mémoire (Maintenant que les types correspondent)
                if (!clesNotifs.Contains((employe.Id, projet.Id)))
                {
                    nouvellesNotifications.Add(new Notification
                    {
                        EmployeId = employe.Id,
                        ProjetId = projet.Id,
                        Message = message,
                        Type = "ProjetFinProche",
                        DateEnvoi = DateTime.Now,
                        Lu = false
                    });
                }
            }
        }

        if (nouvellesNotifications.Any())
        {
            await _context.Notifications.AddRangeAsync(nouvellesNotifications);
            await _context.SaveChangesAsync();
        }
    }
    public async Task<List<Notification>> GetByEmploye(int employeId)
    {
        return await _context.Notifications
            .Where(n => n.EmployeId == employeId)
            .OrderByDescending(n => n.DateEnvoi)
            .ToListAsync();
    }

    public async Task MarkAsRead(int id)
    {
        var notif = await _context.Notifications.FindAsync(id);
        if (notif != null)
        {
            notif.Lu = true;
            await _context.SaveChangesAsync();
        }
    }
}