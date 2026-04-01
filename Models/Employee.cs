namespace Gestor_Equipos.Models
{
    public class Employee
    {
        public int id { get; set; }
        public string name { get; set; }
        public string surname { get; set; }
        public User idAccount { get; set; }
        public Zone zone { get; set; }

    }
}
