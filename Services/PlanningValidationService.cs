using System.Text;
using GestionProjet.DTOs.AI;

namespace GestionProjet.Services;

public class PlanningValidationService
{
    private readonly ILogger<PlanningValidationService> _logger;
    private const decimal MAX_HOURS_PER_DAY = 8m;
    private const int MIN_SUBTASKS_PER_TASK = 1;

    public PlanningValidationService(ILogger<PlanningValidationService> logger)
    {
        _logger = logger;
    }

    public ValidationResultResponse ValidateAndCorrect(ValidatePlanningRequest request)
    {
        var result = new ValidationResultResponse
        {
            IsValid = true,
            Statistics = new ValidationStatisticsDto()
        };

        if (request.Phases == null || request.Phases.Count == 0)
        {
            result.IsValid = false;
            result.Issues.Add(new ValidationIssueDto
            {
                Type = "PlanningVide",
                Severity = "Error",
                Message = "Aucune phase n'a été générée",
                Details = "Le planning doit contenir au moins une phase avec des tâches.",
                AutoCorrected = false
            });
            result.Summary = "Planning invalide : aucune phase générée.";
            return result;
        }

        var holidays = new HashSet<string>(request.JoursFeries ?? new List<string>());
        var memberNames = (request.EquipeDisponible ?? new List<MembreDisponible>())
            .ToDictionary(m => m.Id, m => m.Nom);

        // Tracker pour la redistribution de charge
        var memberDailyLoads = new Dictionary<int, Dictionary<DateTime, decimal>>();

        // Initialiser les charges par membre
        foreach (var member in request.EquipeDisponible ?? new List<MembreDisponible>())
        {
            memberDailyLoads[member.Id] = new Dictionary<DateTime, decimal>();
        }

        var correctedPhases = new List<PhaseCorrigeeDto>();
        int totalTasks = 0;
        int totalSubtasks = 0;

        // ═══════════════════════════════════════════════════════════════
        // ÉTAPE 1 : Validation et correction de base
        // ═══════════════════════════════════════════════════════════════

        foreach (var phase in request.Phases)
        {
            var correctedPhase = new PhaseCorrigeeDto
            {
                TypePhase = phase.TypePhase
            };

            if (phase.Taches == null || phase.Taches.Count == 0)
            {
                result.Issues.Add(new ValidationIssueDto
                {
                    Type = "PhaseVide",
                    Severity = "Warning",
                    Phase = phase.TypePhase,
                    Message = $"La phase '{phase.TypePhase}' ne contient aucune tâche",
                    AutoCorrected = false
                });
                result.Statistics.Warnings++;
                correctedPhases.Add(correctedPhase);
                continue;
            }

            foreach (var task in phase.Taches)
            {
                totalTasks++;
                var correctedTask = new TacheCorrigeeDto
                {
                    Titre = task.Titre,
                    DateDebutPrevue = task.DateDebutPrevue,
                    DateFinPrevue = task.DateFinPrevue,
                    ResponsableId = task.ResponsableId,
                    CompetencesRequises = task.CompetencesRequises ?? new List<string>(),
                    SousTaches = new List<SousTacheCorrigeeDto>()
                };

                // ─── Validation du titre ───
                if (string.IsNullOrWhiteSpace(task.Titre))
                {
                    result.Issues.Add(new ValidationIssueDto
                    {
                        Type = "TitreManquant",
                        Severity = "Error",
                        Phase = phase.TypePhase,
                        Task = "Sans titre",
                        Message = "Tâche sans titre détectée",
                        AutoCorrected = true
                    });
                    correctedTask.Titre = $"Tâche {totalTasks}";
                    correctedTask.CorrectionsApplied.Add("Titre généré automatiquement");
                    correctedTask.WasCorrected = true;
                    result.Statistics.AutoCorrected++;
                    result.Statistics.Errors++;
                }

                // ─── Validation des dates ───
                if (!DateTime.TryParse(task.DateDebutPrevue, out var taskStart) ||
                    !DateTime.TryParse(task.DateFinPrevue, out var taskEnd))
                {
                    result.Issues.Add(new ValidationIssueDto
                    {
                        Type = "DateInvalide",
                        Severity = "Error",
                        Phase = phase.TypePhase,
                        Task = correctedTask.Titre,
                        Message = $"Dates invalides : début='{task.DateDebutPrevue}', fin='{task.DateFinPrevue}'",
                        AutoCorrected = true
                    });

                    // Corriger : utiliser les dates du projet
                    correctedTask.DateDebutPrevue = request.DateDebut.ToString("yyyy-MM-dd");
                    correctedTask.DateFinPrevue = request.DateFinPrevue.ToString("yyyy-MM-dd");
                    correctedTask.CorrectionsApplied.Add("Dates remplacées par les dates du projet");
                    correctedTask.WasCorrected = true;
                    result.Statistics.AutoCorrected++;
                    result.Statistics.Errors++;

                    taskStart = request.DateDebut;
                    taskEnd = request.DateFinPrevue;
                }
                else
                {
                    // Vérifier début < fin
                    if (taskStart > taskEnd)
                    {
                        result.Issues.Add(new ValidationIssueDto
                        {
                            Type = "DateIncoherente",
                            Severity = "Error",
                            Phase = phase.TypePhase,
                            Task = correctedTask.Titre,
                            Message = $"Date de début ({taskStart:yyyy-MM-dd}) après la date de fin ({taskEnd:yyyy-MM-dd})",
                            AutoCorrected = true
                        });

                        // Corriger : inverser les dates
                        correctedTask.DateDebutPrevue = taskEnd.ToString("yyyy-MM-dd");
                        correctedTask.DateFinPrevue = taskStart.ToString("yyyy-MM-dd");
                        correctedTask.CorrectionsApplied.Add("Dates inversées");
                        correctedTask.WasCorrected = true;
                        result.Statistics.AutoCorrected++;
                        result.Statistics.Errors++;

                        (taskStart, taskEnd) = (taskEnd, taskStart);
                    }

                    // Vérifier début >= date projet
                    if (taskStart < request.DateDebut)
                    {
                        result.Issues.Add(new ValidationIssueDto
                        {
                            Type = "DateHorsLimites",
                            Severity = "Error",
                            Phase = phase.TypePhase,
                            Task = correctedTask.Titre,
                            Message = $"Début ({taskStart:yyyy-MM-dd}) avant le début du projet ({request.DateDebut:yyyy-MM-dd})",
                            AutoCorrected = true
                        });

                        correctedTask.DateDebutPrevue = request.DateDebut.ToString("yyyy-MM-dd");
                        correctedTask.CorrectionsApplied.Add($"Début aligné sur le projet ({request.DateDebut:yyyy-MM-dd})");
                        correctedTask.WasCorrected = true;
                        result.Statistics.AutoCorrected++;
                        result.Statistics.Errors++;

                        taskStart = request.DateDebut;
                    }

                    // Vérifier fin <= date projet
                    if (taskEnd > request.DateFinPrevue)
                    {
                        result.Issues.Add(new ValidationIssueDto
                        {
                            Type = "DateHorsLimites",
                            Severity = "Error",
                            Phase = phase.TypePhase,
                            Task = correctedTask.Titre,
                            Message = $"Fin ({taskEnd:yyyy-MM-dd}) après la fin du projet ({request.DateFinPrevue:yyyy-MM-dd})",
                            AutoCorrected = true
                        });

                        correctedTask.DateFinPrevue = request.DateFinPrevue.ToString("yyyy-MM-dd");
                        correctedTask.CorrectionsApplied.Add($"Fin alignée sur le projet ({request.DateFinPrevue:yyyy-MM-dd})");
                        correctedTask.WasCorrected = true;
                        result.Statistics.AutoCorrected++;
                        result.Statistics.Errors++;

                        taskEnd = request.DateFinPrevue;
                    }
                }

                // ─── Validation des sous-tâches ───
                var sousTaches = task.SousTaches ?? new List<SousTacheGenereeInput>();
                totalSubtasks += sousTaches.Count;

                if (sousTaches.Count < MIN_SUBTASKS_PER_TASK)
                {
                    result.Issues.Add(new ValidationIssueDto
                    {
                        Type = "SousTacheManquante",
                        Severity = "Error",
                        Phase = phase.TypePhase,
                        Task = correctedTask.Titre,
                        Message = $"Tâche sans sous-tâche (minimum {MIN_SUBTASKS_PER_TASK} requise)",
                        AutoCorrected = true
                    });

                    // Générer une sous-tâche par défaut
                    var workDays = CountWorkDays(taskStart, taskEnd, holidays);
                    var defaultHours = Math.Min(MAX_HOURS_PER_DAY * workDays, 8m);

                    correctedTask.SousTaches.Add(new SousTacheCorrigeeDto
                    {
                        Titre = $"Réalisation - {correctedTask.Titre}",
                        DureeEstimeeHeures = defaultHours,
                        IsGenerated = true
                    });
                    correctedTask.CorrectionsApplied.Add("Sous-tâche par défaut générée");
                    correctedTask.WasCorrected = true;
                    result.Statistics.AutoCorrected++;
                    result.Statistics.Errors++;
                }
                else
                {
                    // Valider chaque sous-tâche
                    foreach (var st in sousTaches)
                    {
                        var correctedSt = new SousTacheCorrigeeDto
                        {
                            Titre = st.Titre,
                            DureeEstimeeHeures = st.DureeEstimeeHeures,
                            IsGenerated = false
                        };

                        if (string.IsNullOrWhiteSpace(st.Titre))
                        {
                            correctedSt.Titre = $"Sous-tâche de {correctedTask.Titre}";
                            correctedSt.IsGenerated = true;
                            result.Issues.Add(new ValidationIssueDto
                            {
                                Type = "SousTacheSansTitre",
                                Severity = "Warning",
                                Phase = phase.TypePhase,
                                Task = correctedTask.Titre,
                                Message = "Sous-tâche sans titre",
                                AutoCorrected = true
                            });
                            result.Statistics.AutoCorrected++;
                            result.Statistics.Warnings++;
                        }

                        if (st.DureeEstimeeHeures <= 0)
                        {
                            correctedSt.DureeEstimeeHeures = 2m;
                            correctedSt.IsGenerated = true;
                            result.Issues.Add(new ValidationIssueDto
                            {
                                Type = "DureeInvalide",
                                Severity = "Warning",
                                Phase = phase.TypePhase,
                                Task = correctedTask.Titre,
                                Message = $"Sous-tâche '{st.Titre}' avec durée {st.DureeEstimeeHeures}h",
                                AutoCorrected = true
                            });
                            result.Statistics.AutoCorrected++;
                            result.Statistics.Warnings++;
                        }

                        if (st.DureeEstimeeHeures > 40)
                        {
                            // Découper automatiquement les sous-tâches trop longues
                            var splitResult = SplitLongSubtask(correctedSt, correctedTask.Titre);
                            if (splitResult.Count > 1)
                            {
                                result.Issues.Add(new ValidationIssueDto
                                {
                                    Type = "SousTacheTropLongue",
                                    Severity = "Warning",
                                    Phase = phase.TypePhase,
                                    Task = correctedTask.Titre,
                                    Message = $"Sous-tâche de {st.DureeEstimeeHeures}h découpée en {splitResult.Count} parties",
                                    AutoCorrected = true
                                });
                                result.Statistics.AutoCorrected++;
                                result.Statistics.Warnings++;
                                correctedTask.SousTaches.AddRange(splitResult);
                                continue;
                            }
                        }

                        correctedTask.SousTaches.Add(correctedSt);
                    }
                }

ValidateSemanticCompetencies(correctedTask.Titre, correctedTask, phase.TypePhase, result);
      
                var totalTaskHours = correctedTask.SousTaches.Sum(st => st.DureeEstimeeHeures);
                var taskWorkDays = CountWorkDays(taskStart, taskEnd, holidays);
                var dailyContribution = taskWorkDays > 0 ? totalTaskHours / taskWorkDays : 0;

                // Enregistrer la charge prévue
                if (task.ResponsableId.HasValue)
                {
                    for (var d = taskStart; d <= taskEnd; d = d.AddDays(1))
                    {
                        if (IsWorkDay(d, holidays))
                        {
                            if (!memberDailyLoads.ContainsKey(task.ResponsableId.Value))
                            {
                                memberDailyLoads[task.ResponsableId.Value] = new Dictionary<DateTime, decimal>();
                            }
                            memberDailyLoads[task.ResponsableId.Value][d] = 
                                memberDailyLoads[task.ResponsableId.Value].GetValueOrDefault(d) + dailyContribution;
                        }
                    }
                }

                correctedPhase.Taches.Add(correctedTask);
            }

            correctedPhases.Add(correctedPhase);
        }

        // ═══════════════════════════════════════════════════════════════
        // ÉTAPE 2 : Détection et correction des surcharges
        // ═══════════════════════════════════════════════════════════════

        var overloadCorrections = DetectAndFixOverloads(
            correctedPhases, 
            request, 
            memberDailyLoads, 
            holidays,
            result
        );

        if (overloadCorrections > 0)
        {
            result.IsValid = false;
        }

        // ═══════════════════════════════════════════════════════════════
        // ÉTAPE 3 : Validation finale
        // ═══════════════════════════════════════════════════════════════

        var uncorrectedErrors = result.Issues.Count(i => i.Severity == "Error" && !i.AutoCorrected);
        if (uncorrectedErrors > 0)
        {
            result.IsValid = false;
            result.Statistics.Uncorrectable = uncorrectedErrors;
        }

        result.Statistics.TotalIssues = result.Issues.Count;
        result.Statistics.Errors = result.Issues.Count(i => i.Severity == "Error");
        result.Statistics.Warnings = result.Issues.Count(i => i.Severity == "Warning");
        result.Statistics.TasksAnalyzed = totalTasks;
        result.Statistics.SubtasksAnalyzed = totalSubtasks;
        result.Statistics.MembersAnalyzed = request.EquipeDisponible?.Count ?? 0;

        result.CorrectedPhases = correctedPhases;

        // Générer le résumé
        result.Summary = GenerateSummary(result);

        return result;
    }

  private int DetectAndFixOverloads(
    List<PhaseCorrigeeDto> phases,
    ValidatePlanningRequest request,
    Dictionary<int, Dictionary<DateTime, decimal>> memberLoads,
    HashSet<string> holidays,
    ValidationResultResponse result)
{
    int correctionsCount = 0;
    var memberNames = (request.EquipeDisponible ?? new List<MembreDisponible>())
        .ToDictionary(m => m.Id, m => m.Nom);

    foreach (var memberLoad in memberLoads.OrderBy(x => x.Key))
    {
        var memberId = memberLoad.Key;
        var memberName = memberNames.GetValueOrDefault(memberId, $"Membre {memberId}");

        // Identifier toutes les dates où la charge dépasse 8h
        var overloadedDates = memberLoad.Value
            .Where(kvp => kvp.Value > MAX_HOURS_PER_DAY)
            .OrderBy(kvp => kvp.Key)
            .Select(kvp => kvp.Key)
            .ToList();

        if (overloadedDates.Count == 0) continue;

        foreach (var overloadDate in overloadedDates)
        {
            // Trouver les tâches actives de ce membre pour cette date
            var activeTasks = new List<(PhaseCorrigeeDto phase, TacheCorrigeeDto task)>();
            foreach (var phase in phases)
            {
                foreach (var task in phase.Taches.Where(t => t.ResponsableId == memberId))
                {
                    if (DateTime.TryParse(task.DateDebutPrevue, out var tStart) &&
                        DateTime.TryParse(task.DateFinPrevue, out var tEnd))
                    {
                        if (tStart <= overloadDate && tEnd >= overloadDate)
                        {
                            activeTasks.Add((phase, task));
                        }
                    }
                }
            }

            if (activeTasks.Count == 0) continue;

            // ──────────────────────────────────────────────
            // CAS 1 : UNE SEULE TÂCHE SURCHARGÉE (Densité trop élevée)
            // ──────────────────────────────────────────────
            if (activeTasks.Count == 1)
            {
                var (phase, task) = activeTasks[0];
                var tStart = DateTime.Parse(task.DateDebutPrevue);
                var tEnd = DateTime.Parse(task.DateFinPrevue);
                
                // Calculer la charge totale et la densité actuelle
                var totalHours = task.SousTaches.Sum(st => st.DureeEstimeeHeures);
                var currentWorkDays = CountWorkDays(tStart, tEnd, holidays);
                var currentDailyLoad = currentWorkDays > 0 ? totalHours / currentWorkDays : 0;

                if (currentDailyLoad > MAX_HOURS_PER_DAY)
                {
                    // Calculer combien de jours ouvrés sont nécessaires pour rester sous 8h/jour
                    var requiredWorkDays = (int)Math.Ceiling(totalHours / MAX_HOURS_PER_DAY);
                    var additionalDays = requiredWorkDays - currentWorkDays;

                    if (additionalDays > 0)
                    {
                        var originalEnd = task.DateFinPrevue;
                        var newEnd = ExtendWorkDays(tEnd, additionalDays, holidays, request.DateFinPrevue);

                        task.DateFinPrevue = newEnd.ToString("yyyy-MM-dd");
                        task.WasCorrected = true;
                        task.CorrectionsApplied.Add($"Durée étendue (Charge {currentDailyLoad:F1}h/j) : {originalEnd} → {task.DateFinPrevue}");

                        result.Issues.Add(new ValidationIssueDto
                        {
                            Type = "SurchargeDensiteCorrigee",
                            Severity = "Error",
                            Phase = phase.TypePhase,
                            Task = task.Titre,
                            Message = $"Tâche trop dense ({currentDailyLoad:F1}h/j). Durée étendue à {requiredWorkDays} jours pour respecter la limite de 8h.",
                            AutoCorrected = true
                        });

                        result.Statistics.AutoCorrected++;
                        correctionsCount++;
                    }
                }
            }
            // ──────────────────────────────────────────────
            // CAS 2 : PLUSIEURS TÂCHES (Chevauchement / Handoff)
            // ──────────────────────────────────────────────
            else if (activeTasks.Count > 1)
            {
                // Stratégie : On cherche la tâche qui COMMENCE pile le jour de la surcharge (le "handoff")
                var taskToShift = activeTasks.FirstOrDefault(t => 
                    DateTime.Parse(t.task.DateDebutPrevue) == overloadDate
                );

                if (taskToShift.task != null)
                {
                    var originalStart = DateTime.Parse(taskToShift.task.DateDebutPrevue);
                    var nextWorkDay = GetNextWorkDay(originalStart, holidays);
                    
                    // Calculer la nouvelle date de fin en préservant la durée
                    var originalEnd = DateTime.Parse(taskToShift.task.DateFinPrevue);
                    var originalDurationDays = CountWorkDays(originalStart, originalEnd, holidays);
                    var newEnd = ExtendWorkDays(nextWorkDay, originalDurationDays - 1, holidays, request.DateFinPrevue);

                    var oldStartStr = taskToShift.task.DateDebutPrevue;
                    taskToShift.task.DateDebutPrevue = nextWorkDay.ToString("yyyy-MM-dd");
                    taskToShift.task.DateFinPrevue = newEnd.ToString("yyyy-MM-dd");
                    taskToShift.task.WasCorrected = true;
                    taskToShift.task.CorrectionsApplied.Add($"Début décalé (Chevauchement {overloadDate:yyyy-MM-dd}) : {oldStartStr} → {taskToShift.task.DateDebutPrevue}");

                    result.Issues.Add(new ValidationIssueDto
                    {
                        Type = "SurchargeChevauchementCorrigee",
                        Severity = "Error",
                        Phase = taskToShift.phase.TypePhase,
                        Task = taskToShift.task.Titre,
                        Message = $"Tâche décalée pour éviter le chevauchement avec une autre tâche de {memberName}.",
                        AutoCorrected = true
                    });

                    result.Statistics.AutoCorrected++;
                    correctionsCount++;
                }
            }
        }
    }

    return correctionsCount;
}

private DateTime GetNextWorkDay(DateTime date, HashSet<string> holidays)
{
    var nextDay = date.AddDays(1);
    while (!IsWorkDay(nextDay, holidays))
    {
        nextDay = nextDay.AddDays(1);
    }
    return nextDay;
}

    private List<SousTacheCorrigeeDto> SplitLongSubtask(SousTacheCorrigeeDto subtask, string taskTitle)
    {
        var result = new List<SousTacheCorrigeeDto>();
        var maxHoursPerSubtask = 16m; // Max 16h par sous-tâche
        var remainingHours = subtask.DureeEstimeeHeures;
        var partNumber = 1;

        while (remainingHours > 0)
        {
            var hours = Math.Min(remainingHours, maxHoursPerSubtask);
            result.Add(new SousTacheCorrigeeDto
            {
                Titre = $"{subtask.Titre} - Partie {partNumber}",
                DureeEstimeeHeures = hours,
                IsGenerated = true
            });
            remainingHours -= hours;
            partNumber++;
        }

        return result;
    }

    private DateTime ExtendWorkDays(DateTime startDate, int additionalDays, HashSet<string> holidays, DateTime maxDate)
    {
        var current = startDate;
        var daysAdded = 0;

        while (daysAdded < additionalDays && current < maxDate)
        {
            current = current.AddDays(1);
            if (IsWorkDay(current, holidays) && current <= maxDate)
            {
                daysAdded++;
            }
        }

        return current > maxDate ? maxDate : current;
    }

    private List<(DateTime Start, DateTime End)> GroupConsecutiveDates(List<DateTime> dates)
    {
        if (dates.Count == 0) return new List<(DateTime, DateTime)>();

        var sorted = dates.OrderBy(d => d).ToList();
        var ranges = new List<(DateTime Start, DateTime End)>();
        var rangeStart = sorted[0];
        var rangeEnd = sorted[0];

        for (int i = 1; i < sorted.Count; i++)
        {
            if (sorted[i] <= rangeEnd.AddDays(3)) // Tolérance de 3 jours
            {
                rangeEnd = sorted[i];
            }
            else
            {
                ranges.Add((rangeStart, rangeEnd));
                rangeStart = sorted[i];
                rangeEnd = sorted[i];
            }
        }
        ranges.Add((rangeStart, rangeEnd));

        return ranges;
    }

    private int CountWorkDays(DateTime start, DateTime end, HashSet<string> holidays)
    {
        int count = 0;
        for (var d = start; d <= end; d = d.AddDays(1))
        {
            if (IsWorkDay(d, holidays)) count++;
        }
        return Math.Max(1, count);
    }

    private bool IsWorkDay(DateTime date, HashSet<string> holidays)
    {
        if (date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday)
            return false;

        var dateStr = date.ToString("yyyy-MM-dd");
        if (holidays.Contains(dateStr))
            return false;

        return true;
    }

    private string GenerateSummary(ValidationResultResponse result)
    {
        var sb = new StringBuilder();

        if (result.IsValid)
        {
            if (result.Statistics.Warnings > 0)
            {
                sb.Append($"✅ Planning valide avec {result.Statistics.Warnings} avertissement(s).");
            }
            else
            {
                sb.Append("✅ Planning parfaitement valide !");
            }
        }
        else
        {
            sb.Append($"⚠️ Planning corrigé : ");
            sb.Append($"{result.Statistics.Errors} erreur(s) détectée(s), ");
            sb.Append($"{result.Statistics.AutoCorrected} correction(s) automatique(s) appliquée(s).");

            if (result.Statistics.Uncorrectable > 0)
            {
                sb.Append($" {result.Statistics.Uncorrectable} problème(s) nécessite(nt) une intervention manuelle.");
            }
        }

        sb.AppendLine();
        sb.Append($"📊 {result.Statistics.TasksAnalyzed} tâches, {result.Statistics.SubtasksAnalyzed} sous-tâches analysées.");

        return sb.ToString();
    }
    private void ValidateSemanticCompetencies(
    string taskTitle, 
    TacheCorrigeeDto correctedTask, 
    string phaseName, 
    ValidationResultResponse result)
{
    // Liste des mots-clés indiquant une tâche NON CODANTE
    var nonCodingKeywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "analyse", "conception", "architecture", "planification", "documentation",
        "test", "validation", "qualité", "déploiement", "mise en prod", "recette"
    };

    // Liste des langages/technologies interdits pour ces tâches
    var forbiddenTechKeywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "java", "c#", ".net", "python", "php", "javascript", "typescript", "react", 
        "angular", "vue", "sql", "html", "css", "node", "spring", "docker", "kubernetes"
    };

    // Vérifier si le titre de la tâche contient un mot-clé de non-codage
    bool isNonCodingTask = nonCodingKeywords.Any(keyword => taskTitle.Contains(keyword));

    if (isNonCodingTask)
    {
        var skillsToRemove = correctedTask.CompetencesRequises
            .Where(skill => forbiddenTechKeywords.Any(tech => skill.Contains(tech)))
            .ToList();

        if (skillsToRemove.Any())
        {
            var originalList = string.Join(", ", correctedTask.CompetencesRequises);
            
            // Supprimer les compétences interdites
            foreach (var skill in skillsToRemove)
            {
                correctedTask.CompetencesRequises.Remove(skill);
            }

            var newList = string.Join(", ", correctedTask.CompetencesRequises);
            if (string.IsNullOrWhiteSpace(newList)) newList = "(Aucune)";

            // Logger la correction
            result.Issues.Add(new ValidationIssueDto
            {
                Type = "CompetenceIncoherente",
                Severity = "Error",
                Phase = phaseName,
                Task = taskTitle,
                Message = $"Compétences techniques incohérentes pour '{taskTitle}'. Suppression de : {string.Join(", ", skillsToRemove)}.",
                Details = $"Original : {originalList} -> Corrigé : {newList}",
                AutoCorrected = true
            });

            correctedTask.CorrectionsApplied.Add($"Compétences nettoyées (Règle PMP)");
            correctedTask.WasCorrected = true;
            result.Statistics.AutoCorrected++;
        }
    }
}
}