using System.ComponentModel.DataAnnotations;

namespace GestorEquipos.Models
{
    // SEG-09: a minimal audit trail for actions that change who can do what or who can reach
    // what — role changes, account deletion, and remote-access release/reassignment. Deliberately
    // NOT a foreign key to UserSystem: the actor row can be deleted later (see LOG-07's own
    // deletion guard for authored SpecChangeLog rows — this table takes the opposite approach
    // and just snapshots a description instead of blocking deletion) and the audit trail must
    // still read correctly after that.
    public class AuditLogEntry
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public DateTime TimestampUtc { get; set; }

        [Required]
        public int ActorUserSystemId { get; set; }

        [Required]
        [MaxLength(200)]
        public string ActorDescription { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string Action { get; set; } = string.Empty;

        [Required]
        [MaxLength(300)]
        public string TargetDescription { get; set; } = string.Empty;
    }
}
