using KarateTournamentApp.Models;
using System.Collections.Generic;
using System.Linq;
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
            AssignParticipantToCategories(
                p,
                category => category.MinAge >= 18 && category.AllowedBelts.Contains(p.Belt),
                categoryType => CreateParticipantCategory(AllBelts, categoryType, p.Sex, 18, 99));
        }

        public void AssignByBelt(Participant p)
        {
            AssignParticipantToCategories(
                p,
                category => category.MaxAge < 18 && category.AllowedBelts.Contains(p.Belt),
                categoryType => CreateParticipantCategory(p.Belt, categoryType, p.Sex, 1, 17));
        }

        public void AssignByAge(Participant p)
        {
            AssignParticipantToCategories(
                p,
                category => p.Age >= category.MinAge && p.Age <= category.MaxAge,
                categoryType => CreateParticipantCategory(AllBelts, categoryType, p.Sex, p.Age, p.Age));
        }

        public void AssignByBoth(Participant p)
        {
            AssignParticipantToCategories(
                p,
                category => p.Age >= category.MinAge
                    && p.Age <= category.MaxAge
                    && category.AllowedBelts.Contains(p.Belt),
                categoryType => CreateParticipantCategory(p.Belt, categoryType, p.Sex, p.Age, p.Age));
        }

        private void AssignParticipantToCategories(
            Participant participant,
            Func<Category, bool> additionalMatch,
            Func<CategoryType, Category> createCategory)
        {
            foreach (var categoryType in participant.Categories)
            {
                var targetCategory = DefinedCategories.FirstOrDefault(category =>
                    category.CategoryType == categoryType
                    && (participant.Sex == category.Sex || category.Sex == Sex.Unisex)
                    && additionalMatch(category));

                var isNewCategory = targetCategory == null;
                targetCategory ??= createCategory(categoryType);
                targetCategory.Participants.Add(participant);

                if (isNewCategory)
                {
                    DefinedCategories.Add(targetCategory);
                }
            }
        }

        private static Category CreateParticipantCategory(
            List<Belts> belts,
            CategoryType categoryType,
            Sex sex,
            int minAge,
            int maxAge)
        {
            return categoryType == CategoryType.Kumite
                ? new ShobuSanbonCategory(belts, categoryType, sex, minAge, maxAge)
                : new Category(belts, categoryType, sex, minAge, maxAge);
        }

        private static Category CreateParticipantCategory(
            Belts belt,
            CategoryType categoryType,
            Sex sex,
            int minAge,
            int maxAge)
        {
            return categoryType == CategoryType.Kumite
                ? new ShobuSanbonCategory(belt, categoryType, sex, minAge, maxAge)
                : new Category(belt, categoryType, sex, minAge, maxAge);
        }
    }
}