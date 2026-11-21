using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KarateTournamentApp.Models
{
    public class Team : IKataEntrant
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
        public string Name { get; set; }
        string IKataEntrant.JudgingName => Name;
        string IKataEntrant.JudgingDetails => string.Join(", ", Members.Select(member => member.FullName));
        public List<Participant> Members { get; set; } = new List<Participant>();
        public Sex Sex { get; set; }
        public int Age
        {
            get
            {
                return Members.Count > 0 ? Members.Max(m => m.Age) : 0;
            }
        }
        public Team(string name, Sex sex)
        {
            Name = name;
            Sex = sex;
        }
        public void AddMember(Participant p)
        {
            Members.Add(p);
        }  
    }
}
