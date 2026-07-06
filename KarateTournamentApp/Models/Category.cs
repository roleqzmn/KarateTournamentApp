using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Runtime.InteropServices.JavaScript;
using System.Security.Policy;
using System.Text;
using System.Text.Json.Serialization;

namespace KarateTournamentApp.Models
{
    public enum CategoryType
    {
        Kata,
        Kumite,
        Kihon,
        KobudoShort,
        KobudoLong,
        Grappling,
        Team
    }

    [JsonDerivedType(typeof(Category), typeDiscriminator: "base")]
    [JsonDerivedType(typeof(ShobuSanbonCategory), typeDiscriminator: "shobu")]
    [JsonDerivedType(typeof(TeamCategory), typeDiscriminator: "team")]
    public class Category
    {
        public string Name { get; set; } = "";
        public ObservableCollection<Participant> Participants { get; set; } = new ObservableCollection<Participant>();
        public List<Belts> AllowedBelts { get; set; } = new List<Belts> { };
        public List<Match> BracketMatches { get; set; } = new List<Match>();
        public int? MinAge { get; set; }
        public int? MaxAge { get; set; }
        public bool IsFinished { get; set; } = false;
        public Sex Sex { get; set; }
        public CategoryType CategoryType { get; set; }
        public List<(List<decimal> Scores, Guid ParticipantId)> JudgingScores { get; set; } = new List<(List<decimal>, Guid)>();
        public List<ParticipantResult> FinalResults { get; set; } = new List<ParticipantResult>();

        public Category(Belts belt, CategoryType type, Sex sex = Sex.Unisex, int? minAge = null, int? maxAge = null)
        {
            MinAge = minAge;
            MaxAge = maxAge;
            AllowedBelts.Add(belt);
            Sex = sex;
            CategoryType = type;
            GenerateName();
        }

        public Category(List<Belts> belts, CategoryType type, Sex sex = Sex.Unisex, int? minAge = null, int? maxAge = null)
        {
            MinAge = minAge;
            MaxAge = maxAge;
            AllowedBelts = belts ?? new List<Belts>();
            Sex = sex;
            CategoryType = type;
            GenerateName();
        }

        public Category()
        {
        }

        private void GenerateName()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append($"{MinAge}-{MaxAge}");
            foreach(var belt in AllowedBelts)
            {
                sb.Append($", {belt}");
            }
            Name = sb.ToString();
        }
        /// <summary>
        /// Combines the passed category with the current one.
        /// </summary>
        public void MergeWith(Category otherCategory)
        {
            if (otherCategory == null) return;
            foreach (var participant in otherCategory.Participants)
            {
                if (!Participants.Any(p => p.Id == participant.Id))
                {
                    Participants.Add(participant);
                }
            }
            if (Sex != otherCategory.Sex) Sex = Sex.Unisex;
            if (otherCategory.MinAge < this.MinAge) this.MinAge = otherCategory.MinAge;
            if (otherCategory.MaxAge > this.MaxAge) this.MaxAge = otherCategory.MaxAge;

            foreach (var belt in otherCategory.AllowedBelts)
            {
                if (!AllowedBelts.Contains(belt))
                {
                    AllowedBelts.Add(belt);
                }
            }
            if (MinAge >= 18)
            {
                Name = "Senior";
            }
            else
            {
                GenerateName();
            }
        }

        public void PromoteWinner(int currentMatchIndex)
        {
            if (currentMatchIndex == 0) { IsFinished = true; return; }

            int parentIndex = (currentMatchIndex - 1) / 2;
            var winnerId = BracketMatches[currentMatchIndex].WinnerId;
            if (currentMatchIndex % 2 != 0)
                BracketMatches[parentIndex].Aka = winnerId;
            else
                BracketMatches[parentIndex].Shiro = winnerId;
        }

        /// <summary>
        /// Initializes a bracket, semi-randomly
        /// </summary>
        public virtual void InitializeBracket()
        {
            BracketMatches = new List<Match>();
            var size = Participants.Count;

            if (size <= 2)
            {
                var singleMatch = new Match();
                BracketMatches.Add(singleMatch);
                if (size >= 1)
                {
                    singleMatch.Aka = Participants[0].Id;
                }
                if (size >= 2)
                {
                    singleMatch.Shiro = Participants[1].Id;
                }

                return;
            }

            int leafs = 1;
            while (leafs < size) leafs = 2 * leafs;

            int totalMatches = 2 * leafs - 1;
            for (int j = 0; j < totalMatches; j++)
            {
                BracketMatches.Add(new Match());
            }

            var shuffledParticipants = Participants.OrderBy(a => Guid.NewGuid()).ToList(); //pseudo shuffling by guid

            for (int i = 0; i < size; i++)
            {
                int bracketIndex = (leafs - 1) + (i / 2);
                if (i % 2 == 0)
                {
                    BracketMatches[bracketIndex].Aka = shuffledParticipants[i].Id;
                }
                else
                {
                    BracketMatches[bracketIndex].Shiro = shuffledParticipants[i].Id;
                }
            }

            for (int i = leafs - 1; i < totalMatches; i++)
            {
                var match = BracketMatches[i];
                bool hasBye = match.Aka.HasValue ^ match.Shiro.HasValue;
                if (hasBye)
                {
                    match.WinnerId = match.Aka ?? match.Shiro;
                    match.IsFinished = true;
                    PromoteWinner(i);
                }
            }
        }
    }

    public class ShobuSanbonCategory : Category
    {
        public ShobuSanbonCategory(Belts belt, CategoryType type,  Sex sex = Sex.Unisex, int? minAge = null, int? maxAge = null) : base(belt, type, sex, minAge, maxAge)
        {  
        }

        public ShobuSanbonCategory(List<Belts> belts, CategoryType type, Sex sex = Sex.Unisex, int? minAge = null, int? maxAge = null) : base(belts, type, sex, minAge, maxAge)
        {
        }

        public ShobuSanbonCategory() : base()
        {
        }
        /// <summary>
        /// Initializes a bracket, semi-randomly
        /// </summary>
        public override void InitializeBracket()
        {
            BracketMatches = new List<Match>();
            var size = Participants.Count;

            if (size <= 2)
            {
                var singleMatch = new ShobuSanbonMatch();
                BracketMatches.Add(singleMatch);

                if (size >= 1)
                {
                    singleMatch.Aka = Participants[0].Id;
                }
                if (size >= 2)
                {
                    singleMatch.Shiro = Participants[1].Id;
                }

                return;
            }

            int leafs = 1;
            while (leafs < size) leafs = 2 * leafs;

            int totalMatches = 2 * leafs - 1;
            for (int j = 0; j < totalMatches; j++)
            {
                BracketMatches.Add(new ShobuSanbonMatch());
            }

            var shuffledParticipants = Participants.OrderBy(a => Guid.NewGuid()).ToList(); //pseudo shuffling by guid

            for (int i = 0; i < size; i++)
            {
                int bracketIndex = (leafs - 1) + (i / 2);
                if (i % 2 == 0)
                {
                    BracketMatches[bracketIndex].Aka = shuffledParticipants[i].Id;
                }
                else
                {
                    BracketMatches[bracketIndex].Shiro = shuffledParticipants[i].Id;
                }
            }

            for (int i = leafs - 1; i < totalMatches; i++)
            {
                var match = BracketMatches[i];
                bool hasBye = match.Aka.HasValue ^ match.Shiro.HasValue;
                if (hasBye)
                {
                    match.WinnerId = match.Aka ?? match.Shiro;
                    match.IsFinished = true;
                    PromoteWinner(i);
                }
            }
        }
    }

    public class TeamCategory : Category
    {
        public ObservableCollection<Team> Teams { get; set; } = new ObservableCollection<Team>();
        public TeamCategory(List<Belts> belts, CategoryType type, Sex sex = Sex.Unisex, int? minAge = null, int? maxAge = null) : base(belts, type, sex, minAge, maxAge)
        {
            GenerateName();
        }
        public TeamCategory() : base()
        {
        }
        private void GenerateName()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append($"Team Kata {MinAge}-{MaxAge}");
            foreach (var belt in AllowedBelts)
            {
                sb.Append($", {belt}");
            }
            Name = sb.ToString();
        }
        /// <summary>
        /// Combines the passed category with the current one.
        /// </summary>
        public void MergeWith(TeamCategory otherCategory)
        {
            if (otherCategory == null) return;
            foreach (var team in otherCategory.Teams)
            {
                if (!Teams.Any(t => t.Id == team.Id))
                {
                    Teams.Add(team);
                }
            }
            if (Sex != otherCategory.Sex) Sex = Sex.Unisex;
            if (otherCategory.MinAge < this.MinAge) this.MinAge = otherCategory.MinAge;
            if (otherCategory.MaxAge > this.MaxAge) this.MaxAge = otherCategory.MaxAge;

            foreach (var belt in otherCategory.AllowedBelts)
            {
                if (!AllowedBelts.Contains(belt))
                {
                    AllowedBelts.Add(belt);
                }
            }
            if (MinAge >= 18)
            {
                Name = "Senior Team Kata";
            }
            else
            {
                GenerateName();
            }
        }
    }
}