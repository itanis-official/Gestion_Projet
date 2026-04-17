using GestionProjet.Data;
using GestionProjet.Enums;
using Microsoft.EntityFrameworkCore;

public class StatutService
{
    private readonly ApplicationDbContext _context;

    public StatutService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task MettreAJourStatuts(int tacheId)
    {
        var tache = await _context.Taches
            .Include(t => t.SousTaches)
            .Include(t => t.Phase)
                .ThenInclude(p => p.Taches)
                    .ThenInclude(t => t.SousTaches)
            .Include(t => t.Phase)
                .ThenInclude(p => p.Projet)
                    .ThenInclude(pr => pr.Phases)
                        .ThenInclude(ph => ph.Taches)
            .FirstOrDefaultAsync(t => t.Id == tacheId);

        if (tache == null) return;

       
        if (tache.SousTaches.All(st => st.Statut == StatutSousTache.Validee))
            tache.Statut = StatutTache.Terminee;
        else if (tache.SousTaches.Any(st => st.Statut == StatutSousTache.EnCours || st.Statut == StatutSousTache.ATester))
            tache.Statut = StatutTache.EnCours;
        else
            tache.Statut = StatutTache.ADemarrer;

        
        var phase = tache.Phase;

        if (phase.Taches.All(t => t.Statut == StatutTache.Terminee))
            phase.Statut = StatutPhase.Terminee;
        else if (phase.Taches.Any(t => t.Statut == StatutTache.EnCours))
            phase.Statut = StatutPhase.EnCours;
        else
            phase.Statut = StatutPhase.ADemarrer;

        var projet = phase.Projet;

        if (projet.Phases.All(p => p.Statut == StatutPhase.Terminee))
            projet.Statut = StatutProjet.Termine;
        else if (projet.Phases.Any(p => p.Statut == StatutPhase.EnCours))
            projet.Statut = StatutProjet.EnCours;
        else
            projet.Statut = StatutProjet.Planifie;

        await _context.SaveChangesAsync();
    }
}