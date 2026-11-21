using KarateTournamentApp.Models;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace KarateTournamentApp.Services
{
    public class CategoryManager
    {
        public List<Category> DefinedCategories { get; set; } = new List<Category>();
        private List<Belts> AllBelts { get; } = Enum.GetValues<Belts>().ToList();
        public void AssignParticipant(Participant p, bool DivideByBelt, bool DivideByAge) 
        {
            if (p.Age >= ParticipantCategoryRules.SeniorAge) { AssignSenior(p); return; }

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
            AssignParticipantToCategories(p, CategoryGrouping.AgeAndBelt, senior: true);
        }

        public void AssignByBelt(Participant p)
        {
            AssignParticipantToCategories(p, CategoryGrouping.Belt, senior: false);
        }

        public void AssignByAge(Participant p)
        {
            AssignParticipantToCategories(p, CategoryGrouping.Age, senior: false);
        }

        public void AssignByBoth(Participant p)
        {
            AssignParticipantToCategories(p, CategoryGrouping.AgeAndBelt, senior: false);
        }

        private void AssignParticipantToCategories(
            Participant participant, CategoryGrouping grouping, bool senior)
        {
            var (minAge, maxAge, singleBelt) = ParticipantCategoryRules.GetSettings(
                participant.Age, participant.Belt, grouping, senior);
            foreach (var categoryType in participant.Categories)
            {
                var targetCategory = DefinedCategories.FirstOrDefault(category =>
                    category.CategoryType == categoryType
                    && ParticipantCategoryRules.Matches(participant.Age, participant.Sex, senior, grouping,
                        category.MinAge, category.MaxAge, category.Sex, category.AllowedBelts.Contains(participant.Belt)));

                var isNewCategory = targetCategory == null;
                targetCategory ??= CreateParticipantCategory(
                    singleBelt is Belts belt ? [belt] : [.. AllBelts], categoryType, participant.Sex, minAge, maxAge);
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
            return categoryType.UsesMatchBracket()
                ? new ShobuSanbonCategory(belts, categoryType, sex, minAge, maxAge)
                : new Category(belts, categoryType, sex, minAge, maxAge);
        }

    }
}
