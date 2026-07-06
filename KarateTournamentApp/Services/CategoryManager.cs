using KarateTournamentApp.Models;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Xml.Linq;

namespace KarateTournamentApp.Services
{
    public class CategoryManager
    {
        public List<Category> DefinedCategories { get; set; } = new List<Category>();
        private List<Belts> AllBelts { get; } = Enum.GetValues<Belts>().ToList();
        public void AssignParticipant(Participant p, bool DivideByBelt, bool DivideByAge) 
        {
            if (p.Age >= 18){ AssignSenior(p); return; }

            if (DivideByBelt && DivideByAge)
            {
                AssignByBoth(p);
                return;
            }

            if (DivideByAge)
            {
                AssignByAge(p);
                return;
            }

            if (DivideByBelt)
            {
                AssignByBelt(p);
            }
        }
        public void AssignTeam(Team t)
        {
            var targetCategory = (TeamCategory?)DefinedCategories.FirstOrDefault(c =>
                    c.MinAge >= 18 &&
                    (t.Sex == c.Sex || c.Sex == Sex.Unisex) &&
                    CategoryType.Team == c.CategoryType);
            if(targetCategory != null)
            {
                targetCategory.Teams.Add(t);
            }
            else
            {
                var cat = new TeamCategory(AllBelts, CategoryType.Team, t.Sex, t.Age, t.Age);
                cat.Teams.Add(t);
                DefinedCategories.Add(cat);
            }
        }
        public void AssignSenior(Participant p)
        {
            foreach(var categoryType in p.Categories)
            {
                var targetCategory = DefinedCategories.FirstOrDefault(c =>
                   c.MinAge >= 18 &&
                   c.AllowedBelts.Contains(p.Belt) &&
                   (p.Sex == c.Sex || c.Sex == Sex.Unisex) &&
                   categoryType == c.CategoryType);

                if (targetCategory != null)
                {
                    targetCategory.Participants.Add(p);
                }
                else
                {
                    var cat = new Category(AllBelts, categoryType, p.Sex, 18, 99);
                    if (categoryType == CategoryType.Kumite)
                    {
                        cat = new ShobuSanbonCategory(AllBelts, categoryType, p.Sex, 18, 99);
                    }
                    cat.Participants.Add(p);
                    DefinedCategories.Add(cat);
                }
            }
        }
        public void AssignByBelt(Participant p)
        {
            foreach (var categoryType in p.Categories)
            {
                var targetCategory = DefinedCategories.FirstOrDefault(c =>
                c.MaxAge < 18 &&
                c.AllowedBelts.Contains(p.Belt) &&
                (p.Sex == c.Sex || c.Sex == Sex.Unisex) &&
                categoryType == c.CategoryType);

                if (targetCategory != null)
                {
                    targetCategory.Participants.Add(p);
                }
                else
                {
                    var cat = new Category(p.Belt, categoryType, p.Sex, 1, 17);
                    if (categoryType == CategoryType.Kumite)
                    {
                        cat = new ShobuSanbonCategory(p.Belt, categoryType, p.Sex, 1, 17);
                    }
                    cat.Participants.Add(p);
                    DefinedCategories.Add(cat);
                }
            }
        }
        public void AssignByAge(Participant p)
        {
            foreach (var categoryType in p.Categories)
            {
                var targetCategory = DefinedCategories.FirstOrDefault(c =>
                    p.Age >= c.MinAge &&
                    p.Age <= c.MaxAge &&
                    (p.Sex == c.Sex || c.Sex == Sex.Unisex) &&
                    categoryType == c.CategoryType);

                if (targetCategory != null)
                {
                    targetCategory.Participants.Add(p);
                }
                else
                { 
                    var cat = new Category(AllBelts, categoryType, p.Sex, p.Age, p.Age);
                    if (categoryType == CategoryType.Kumite)
                    {
                        cat = new ShobuSanbonCategory(AllBelts, categoryType, p.Sex, p.Age, p.Age);
                    }
                    cat.Participants.Add(p);
                    DefinedCategories.Add(cat);
                }
            }
        }
        public void AssignByBoth(Participant p)
        {
            foreach (var categoryType in p.Categories)
            {
                var targetCategory = DefinedCategories.FirstOrDefault(c =>
                    p.Age >= c.MinAge &&
                    p.Age <= c.MaxAge &&
                    c.AllowedBelts.Contains(p.Belt) &&
                    (p.Sex == c.Sex || c.Sex == Sex.Unisex) &&
                    categoryType == c.CategoryType);

                if (targetCategory != null)
                {
                    targetCategory.Participants.Add(p);
                }
                else
                {
                    StringBuilder sb = new StringBuilder();
                    sb.Append(p.Belt.ToString());
                    sb.Append(" ");
                    sb.Append(p.Age.ToString());
                    string name = sb.ToString();
                    var cat = new Category(p.Belt, categoryType, p.Sex, p.Age, p.Age);
                    if (categoryType == CategoryType.Kumite)
                    {
                        cat = new ShobuSanbonCategory(p.Belt, categoryType, p.Sex, p.Age, p.Age);
                    }
                    cat.Participants.Add(p);
                    DefinedCategories.Add(cat);
                }
            }
        }
        
            
    }
}