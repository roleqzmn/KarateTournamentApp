using static KarateClassLibrary.Enums;

namespace KarateTournament.Api.Entities
{
    public class Match
    {
        public int Id { get; set; }
        public int? Aka { get; set; }
        public int? Shiro { get; set; }
        public int? WinnerId { get; set; }
        public short AkaScore { get; set; }
        public short ShiroScore { get; set; }
        public bool IsFinished { get; set; }
        public bool IsDisqualification { get; set; }
    }
}
