using static KarateClassLibrary.Enums;

namespace KarateTournament.Api.Entities
{
    public class Team
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public List<Participant> Members { get; set; } = new List<Participant>();
        public Sex Sex { get; set; }
        public int Age { get; set;  }
    }
}
