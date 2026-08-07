using static KarateClassLibrary.Enums;

namespace KarateTournament.Api.Entities
{
    public class Participant
    {
        public int Id { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public int Age { get; set; }
        public string? Club { get; set; }
        public Belts Belt { get; set; }
        public List<int> MatchHistory { get; set; }
        public string FullName { get; set; }
        public Sex Sex { get; set; }
        public List<CategoryType> Categories { get; set; }
    }
}
