using System.Text.Json.Serialization;

namespace KarateTournamentApp.Models
{
    /// <summary>
    /// Class containing all necessary participant info
    /// </summary>
    public class Participant
    {
        private int _id = LocalIdGenerator.Next();

        public int Id
        {
            get => _id;
            set
            {
                _id = value;
                LocalIdGenerator.Reserve(value);
            }
        }
        public string FirstName { get ; set; }
        public string LastName { get; set; }
        public int Age { get; set; }
        public string? Club { get; set; }
        public Belts Belt { get; set; }
        public List<int> MatchHistory { get; set; } = new List<int>();
        public string FullName => $"{FirstName} {LastName}";
        public Sex Sex { get ; set; }
        public List<CategoryType> Categories { get; set; }

        public Participant(string firstName, string lastName, int age, Belts belt, Sex sex, List<CategoryType> categories, string? club = null)
        {
            FirstName = firstName;
            LastName = lastName;
            Age = age;
            Belt = belt;
            Club = club;
            Sex = sex;
            Categories = categories;
        }

#pragma warning disable CS8618
        public Participant()
#pragma warning restore CS8618
        {
        }
    }
}
