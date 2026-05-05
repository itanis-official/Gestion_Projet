using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GestionProjet.Models

{
    [Table("TypesProjet")]
    public class TypeProjet
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public Guid TypeProjetGuid { get; set; }

        [Required]
        [MaxLength(255)] 
        public string Value { get; set; } = "";

        [Required]
        [MaxLength(255)] 
        public string Label { get; set; } = "";

        public bool IsActive { get; set; }
        public int Ordre { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}