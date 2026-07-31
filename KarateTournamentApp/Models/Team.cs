using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KarateTournamentApp.Models
{
    public class Team
    {
        public int Id { get; set; } 
        public string Name { get; set; }
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
