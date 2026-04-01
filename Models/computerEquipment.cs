namespace Gestor_Equipos.Models
{
    public class computerEquipment
    {
        public int id { get; set; }
        public string model { get; set; }
        public string serial { get; set; }

        public State stateName { get; set; }
    }
}
