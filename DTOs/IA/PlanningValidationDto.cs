// GestionProjet/DTOs/AI/PlanningValidationDto.cs
using System.Text.Json.Serialization;

namespace GestionProjet.DTOs.AI;

public class ValidatePlanningRequest
{
    public string ProjetNom { get; set; } = "";
    public DateTime DateDebut { get; set; }
    public DateTime DateFinPrevue { get; set; }
    public List<MembreDisponible>? EquipeDisponible { get; set; }
    public List<string>? JoursFeries { get; set; }
    public List<PhaseGenereeInput>? Phases { get; set; }
}

public class PhaseGenereeInput
{
    public string TypePhase { get; set; } = "";
    public List<TacheGenereeInput>? Taches { get; set; }
}

public class TacheGenereeInput
{
    public string Titre { get; set; } = "";
    public string DateDebutPrevue { get; set; } = "";
    public string DateFinPrevue { get; set; } = "";
    public int? ResponsableId { get; set; }
    public List<string>? CompetencesRequises { get; set; }
    public List<SousTacheGenereeInput>? SousTaches { get; set; }
}

public class SousTacheGenereeInput
{
    public string Titre { get; set; } = "";
    public decimal DureeEstimeeHeures { get; set; }
}

public class ValidationResultResponse
{
    [JsonPropertyName("isValid")]
    public bool IsValid { get; set; }
    
    [JsonPropertyName("issues")]
    public List<ValidationIssueDto> Issues { get; set; } = new();
    
    [JsonPropertyName("corrections")]
    public List<CorrectionDto> Corrections { get; set; } = new();
    
    [JsonPropertyName("correctedPhases")]
    public List<PhaseCorrigeeDto>? CorrectedPhases { get; set; }
    
    [JsonPropertyName("statistics")]
    public ValidationStatisticsDto Statistics { get; set; } = new();
    
    [JsonPropertyName("summary")]
    public string Summary { get; set; } = "";
}

public class ValidationIssueDto
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = ""; // DateHorsLimites, SurchargeMembre, SousTacheManquante, etc.
    
    [JsonPropertyName("severity")]
    public string Severity { get; set; } = ""; // Error, Warning, Info
    
    [JsonPropertyName("phase")]
    public string Phase { get; set; } = "";
    
    [JsonPropertyName("task")]
    public string Task { get; set; } = "";
    
    [JsonPropertyName("memberId")]
    public int? MemberId { get; set; }
    
    [JsonPropertyName("memberName")]
    public string? MemberName { get; set; }
    
    [JsonPropertyName("message")]
    public string Message { get; set; } = "";
    
    [JsonPropertyName("details")]
    public string? Details { get; set; }
    
    [JsonPropertyName("autoCorrected")]
    public bool AutoCorrected { get; set; }
}

public class CorrectionDto
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "";
    
    [JsonPropertyName("phase")]
    public string Phase { get; set; } = "";
    
    [JsonPropertyName("task")]
    public string Task { get; set; } = "";
    
    [JsonPropertyName("field")]
    public string Field { get; set; } = "";
    
    [JsonPropertyName("originalValue")]
    public string OriginalValue { get; set; } = "";
    
    [JsonPropertyName("correctedValue")]
    public string CorrectedValue { get; set; } = "";
    
    [JsonPropertyName("reason")]
    public string Reason { get; set; } = "";
}

public class PhaseCorrigeeDto
{
    [JsonPropertyName("typePhase")]
    public string TypePhase { get; set; } = "";
    
    [JsonPropertyName("taches")]
    public List<TacheCorrigeeDto> Taches { get; set; } = new();
}

public class TacheCorrigeeDto
{
    [JsonPropertyName("titre")]
    public string Titre { get; set; } = "";
    
    [JsonPropertyName("dateDebutPrevue")]
    public string DateDebutPrevue { get; set; } = "";
    
    [JsonPropertyName("dateFinPrevue")]
    public string DateFinPrevue { get; set; } = "";
    
    [JsonPropertyName("responsableId")]
    public int? ResponsableId { get; set; }
    
    [JsonPropertyName("competencesRequises")]
    public List<string> CompetencesRequises { get; set; } = new();
    
    [JsonPropertyName("sousTaches")]
    public List<SousTacheCorrigeeDto> SousTaches { get; set; } = new();
    
    [JsonPropertyName("wasCorrected")]
    public bool WasCorrected { get; set; }
    
    [JsonPropertyName("correctionsApplied")]
    public List<string> CorrectionsApplied { get; set; } = new();
}

public class SousTacheCorrigeeDto
{
    [JsonPropertyName("titre")]
    public string Titre { get; set; } = "";
    
    [JsonPropertyName("dureeEstimeeHeures")]
    public decimal DureeEstimeeHeures { get; set; }
    
    [JsonPropertyName("isGenerated")]
    public bool IsGenerated { get; set; }
}

public class ValidationStatisticsDto
{
    [JsonPropertyName("totalIssues")]
    public int TotalIssues { get; set; }
    
    [JsonPropertyName("errors")]
    public int Errors { get; set; }
    
    [JsonPropertyName("warnings")]
    public int Warnings { get; set; }
    
    [JsonPropertyName("autoCorrected")]
    public int AutoCorrected { get; set; }
    
    [JsonPropertyName("uncorrectable")]
    public int Uncorrectable { get; set; }
    
    [JsonPropertyName("tasksAnalyzed")]
    public int TasksAnalyzed { get; set; }
    
    [JsonPropertyName("subtasksAnalyzed")]
    public int SubtasksAnalyzed { get; set; }
    
    [JsonPropertyName("membersAnalyzed")]
    public int MembersAnalyzed { get; set; }
}