using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using GestionProjet.Data;
using GestionProjet.Models;
using GestionProjet.Enums;

namespace GestionProjet.Services
{
    public class PlanningIAService
    {
        private readonly ApplicationDbContext _context;
        private readonly NotificationService _notificationService;
        private readonly HttpClient _httpClient;

        public PlanningIAService(ApplicationDbContext context, NotificationService notificationService)
        {
            _context = context;
            _notificationService = notificationService;
            _httpClient = new HttpClient();
        }

        public async Task<PlanningGenere> GenererPlanningOptimise(Projet projet, PlanningIARequest request)
        {
            var donneesProjet = await CollecterDonneesProjet(projet);
            
            var competencesEquipe = await AnalyserCompetencesEquipe(projet);
            
            var chargeActuelle = await CalculerChargeActuelle(projet.GroupeEquipe?.Employes?.ToList() ?? new List<Employe>());
            
            var planning = GenererPlanningAvecAlgorithmes(donneesProjet, competencesEquipe, chargeActuelle, request);
            
            var planningValide = await ValiderEtAjusterPlanning(planning, request);
            
            return planningValide;
        }

        private async Task<DonneesProjetIA> CollecterDonneesProjet(Projet projet)
        {
            return new DonneesProjetIA
            {
                Nom = projet.Nom,
                TypeProjet = projet.TypeProjet.ToString(),
                DateDebut = projet.DateDebut,
                DateFinPrevue = projet.DateFinPrevue,
                BudgetEstime = (double)projet.BudgetEstime,
                Phases = projet.Phases.Select(p => new PhaseIA
                {
                    Type = p.TypePhase.ToString(),
                    Taches = p.Taches.Select(t => new TacheIA
                    {
                        Titre = t.Titre,
                        DureeEstimee = (double)(t.SousTaches?.Sum(st => st.DureeEstimeeHeures) ?? 0),
                        SousTaches = t.SousTaches?.Select(st => new SousTacheIA
                        {
                            Titre = st.Titre,
                            DureeHeures = (double)st.DureeEstimeeHeures,
                            Dependances = new List<string>()
                        }).ToList() ?? new List<SousTacheIA>()
                    }).ToList()
                }).ToList()
            };
        }

        private async Task<Dictionary<int, List<string>>> AnalyserCompetencesEquipe(Projet projet)
        {
            var competences = new Dictionary<int, List<string>>();
            
            if (projet.GroupeEquipe?.Employes != null)
            {
                foreach (var employe in projet.GroupeEquipe.Employes)
                {
                    var comps = await _context.EmployeCompetences
                        .Where(ce => ce.EmployeId == employe.Id)
                        .Select(ce => ce.Competence.Nom)
                        .ToListAsync();
                    
                    competences[employe.Id] = comps;
                }
            }
            
            return competences;
        }

        private async Task<Dictionary<int, double>> CalculerChargeActuelle(List<Employe> employes)
        {
            var charge = new Dictionary<int, double>();
            
            foreach (var employe in employes)
            {
                var tachesEnCours = await _context.Taches
                    .Where(t => (t.ResponsableId == employe.Id || t.TesteurId == employe.Id)
                                && t.DateFinPrevue > DateTime.Now
                                && t.Statut != StatutTache.Terminee)
                    .Include(t => t.SousTaches)
                    .ToListAsync();
                
                var heuresRestantes = tachesEnCours.Sum(t => 
                    t.SousTaches != null 
                        ? (double)t.SousTaches.Where(st => st.Statut != StatutSousTache.Validee)
                                     .Sum(st => st.DureeEstimeeHeures)
                        : 0);
                
                charge[employe.Id] = heuresRestantes;
            }
            
            return charge;
        }

        private PlanningGenere GenererPlanningAvecAlgorithmes(
            DonneesProjetIA projet, 
            Dictionary<int, List<string>> competences,
            Dictionary<int, double> chargeActuelle,
            PlanningIARequest request)
        {
            var planning = new PlanningGenere();
            var assignations = new List<AssignationTache>();
            
            var charge = new Dictionary<int, double>(chargeActuelle);
            
            foreach (var phase in projet.Phases)
            {
                foreach (var tache in phase.Taches)
                {
                    var meilleurCandidat = TrouverMeilleurCandidat(tache, competences, charge, projet, request);
                    
                    if (meilleurCandidat != null)
                    {
                        var datesOptimales = CalculerDatesOptimales(tache, phase, projet.DateDebut, request);
                        
                        assignations.Add(new AssignationTache
                        {
                            TacheId = tache.Titre,
                            Phase = phase.Type,
                            ResponsableId = meilleurCandidat.EmployeId,
                            DateDebut = datesOptimales.DateDebut,
                            DateFin = datesOptimales.DateFin,
                            ScoreCompatibilite = meilleurCandidat.Score
                        });
                        
                        if (charge.ContainsKey(meilleurCandidat.EmployeId))
                            charge[meilleurCandidat.EmployeId] += tache.DureeEstimee;
                        else
                            charge[meilleurCandidat.EmployeId] = tache.DureeEstimee;
                    }
                }
            }
            
            planning.NouvellesAssignations = assignations;
            planning.PhasesGenerees = projet.Phases;
            planning.Resume = GenerateResume(assignations, projet);
            
            return planning;
        }

        private Candidat TrouverMeilleurCandidat(
            TacheIA tache,
            Dictionary<int, List<string>> competences,
            Dictionary<int, double> chargeActuelle,
            DonneesProjetIA projet,
            PlanningIARequest request)
        {
            var candidats = new List<Candidat>();
            
            foreach (var employe in competences.Keys)
            {
                double score = 0;
                
                if (request.TenirCompteCompetences)
                {
                    var competencesEmploye = competences[employe];
                    var competencesRequises = tache.CompetencesRequises ?? new List<string>();
                    
                    if (competencesRequises.Any())
                    {
                        var match = competencesRequises.Count(cr => 
                            competencesEmploye.Any(ce => ce.ToLower().Contains(cr.ToLower())));
                        score += (double)match / competencesRequises.Count * 50;
                    }
                    else
                    {
                        score += 25;
                    }
                }
                
                if (request.OptimiserChargeEquipe)
                {
                    var charge = chargeActuelle.GetValueOrDefault(employe, 0);
                    var chargeIdeale = tache.DureeEstimee / Math.Max(1, projet.Phases.Count * 2);
                    if (chargeIdeale > 0)
                        score += Math.Max(0, 30 - (charge / chargeIdeale) * 10);
                    else
                        score += 15;
                }
                
                score += CalculerScorePriorite(request.Priorite, tache);
                
                candidats.Add(new Candidat
                {
                    EmployeId = employe,
                    Score = Math.Min(100, Math.Max(0, score))
                });
            }
            
            return candidats.OrderByDescending(c => c.Score).FirstOrDefault();
        }

        private double CalculerScorePriorite(string priorite, TacheIA tache)
        {
            switch (priorite)
            {
                case "Delai":
                    return tache.DureeEstimee < 20 ? 20 : 10;
                case "Qualite":
                    return tache.SousTaches != null && tache.SousTaches.Count > 3 ? 20 : 10;
                default: 
                    return 15;
            }
        }

        private (DateTime DateDebut, DateTime DateFin) CalculerDatesOptimales(
            TacheIA tache, 
            PhaseIA phase, 
            DateTime dateDebutProjet,
            PlanningIARequest request)
        {
           
            var dateDebut = dateDebutProjet;
            
            var indexPhase = 0; 
            dateDebut = dateDebut.AddDays(indexPhase * 3);
            
            var heuresTotal = tache.DureeEstimee;
            var joursNecessaires = Math.Ceiling(heuresTotal / request.MaxHeuresParJour);
            
            var dateFin = dateDebut.AddDays(joursNecessaires);
            
            return (dateDebut, dateFin);
        }

        private async Task<PlanningGenere> ValiderEtAjusterPlanning(PlanningGenere planning, PlanningIARequest request)
        {
            var conflits = new List<Conflit>();
            
            foreach (var assignation in planning.NouvellesAssignations)
            {
                var chevauchements = await DetecterChevauchements(assignation);
                if (chevauchements.Any())
                {
                    conflits.AddRange(chevauchements);
                }
            }
            
            if (conflits.Any())
            {
                planning = AjusterDatesConflits(planning, conflits, request);
            }
            
            return planning;
        }

        private async Task<List<Conflit>> DetecterChevauchements(AssignationTache assignation)
        {
            var conflits = new List<Conflit>();
            
            var tachesExistantes = await _context.Taches
                .Where(t => (t.ResponsableId == assignation.ResponsableId || t.TesteurId == assignation.ResponsableId)
                            && t.DateDebutPrevue <= assignation.DateFin
                            && t.DateFinPrevue >= assignation.DateDebut
                            && t.Statut != StatutTache.Terminee)
                .ToListAsync();
            
            foreach (var tache in tachesExistantes)
            {
                conflits.Add(new Conflit
                {
                    EmployeId = assignation.ResponsableId,
                    TacheExistante = tache.Titre,
                    DateConflit = assignation.DateDebut,
                    Type = "Chevauchement"
                });
            }
            
            return conflits;
        }

        private PlanningGenere AjusterDatesConflits(PlanningGenere planning, List<Conflit> conflits, PlanningIARequest request)
        {
            var conflitsParEmploye = conflits.GroupBy(c => c.EmployeId);
            
            foreach (var groupe in conflitsParEmploye)
            {
                var assignationsEmploye = planning.NouvellesAssignations
                    .Where(a => a.ResponsableId == groupe.Key)
                    .OrderBy(a => a.DateDebut)
                    .ToList();
                
                for (int i = 1; i < assignationsEmploye.Count; i++)
                {
                    var precedent = assignationsEmploye[i - 1];
                    var courant = assignationsEmploye[i];
                    
                    if (courant.DateDebut < precedent.DateFin.AddDays(1))
                    {
                        var nouveauDebut = precedent.DateFin.AddDays(1);
                        var duree = (courant.DateFin - courant.DateDebut).Days;
                        courant.DateDebut = nouveauDebut;
                        courant.DateFin = nouveauDebut.AddDays(duree);
                    }
                }
            }
            
            return planning;
        }

        private string GenerateResume(List<AssignationTache> assignations, DonneesProjetIA projet)
        {
            var totalTaches = assignations.Count;
            var scoreMoyen = assignations.Any() ? assignations.Average(a => a.ScoreCompatibilite) : 0;
            var dureeTotale = assignations.Any() 
                ? (assignations.Max(a => a.DateFin) - assignations.Min(a => a.DateDebut)).Days 
                : 0;
            
            return $"Planning généré avec {totalTaches} tâches assignées, score de compatibilité moyen de {scoreMoyen:F1}%, durée totale estimée de {dureeTotale} jours.";
        }
    }

    public class DonneesProjetIA
    {
        public string Nom { get; set; } = string.Empty;
        public string TypeProjet { get; set; } = string.Empty;
        public DateTime DateDebut { get; set; }
        public DateTime DateFinPrevue { get; set; }
        public double BudgetEstime { get; set; }
        public List<PhaseIA> Phases { get; set; } = new();
    }

    public class PhaseIA
    {
        public string Type { get; set; } = string.Empty;
        public List<TacheIA> Taches { get; set; } = new();
    }

    public class TacheIA
    {
        public string Titre { get; set; } = string.Empty;
        public double DureeEstimee { get; set; }
        public List<SousTacheIA> SousTaches { get; set; } = new();
        public List<string> CompetencesRequises { get; set; } = new();
    }

    public class SousTacheIA
    {
        public string Titre { get; set; } = string.Empty;
        public double DureeHeures { get; set; }
        public List<string> Dependances { get; set; } = new();
    }

    public class PlanningGenere
    {
        public List<AssignationTache> NouvellesAssignations { get; set; } = new();
        public List<PhaseIA> PhasesGenerees { get; set; } = new();
        public string Resume { get; set; } = string.Empty;
    }

    public class AssignationTache
    {
        public string TacheId { get; set; } = string.Empty;
        public string Phase { get; set; } = string.Empty;
        public int ResponsableId { get; set; }
        public DateTime DateDebut { get; set; }
        public DateTime DateFin { get; set; }
        public double ScoreCompatibilite { get; set; }
    }

    public class Candidat
    {
        public int EmployeId { get; set; }
        public double Score { get; set; }
    }

    public class Conflit
    {
        public int EmployeId { get; set; }
        public string TacheExistante { get; set; } = string.Empty;
        public DateTime DateConflit { get; set; }
        public string Type { get; set; } = string.Empty;
    }

    public class PlanningIARequest
    {
        public string Priorite { get; set; } = "Equilibre";
        public bool OptimiserChargeEquipe { get; set; } = true;
        public bool TenirCompteCompetences { get; set; } = true;
        public int MaxHeuresParJour { get; set; } = 7;
    }
}