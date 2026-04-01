
namespace Gestor_Equipos.Models
{
    public class AssetAssignment
    {
        public int id { get; set; }
        public DateTime issueDate {get; set;}
        public DateTime returnDate { get; set; }
        public string observation   { get; set; }
    }
}
