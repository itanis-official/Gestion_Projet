namespace GestionProjet.DTOs;

public class NotificationDto
{
    public int Id { get; set; }
    public int EmployeId { get; set; }
    public string Message { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public DateTime DateEnvoi { get; set; }
    public bool Lu { get; set; }
}

public class NotificationDetailDto : NotificationDto
{
    public string EmployeNom { get; set; } = string.Empty;
}