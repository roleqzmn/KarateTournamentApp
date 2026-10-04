using System;
using System.Collections.Generic;
using KarateTournamentApp.Models;

namespace KarateTournamentApp.Services
{
    internal static class ParticipantImportParser
    {
        public static Participant? TryCreateParticipant(
            string? firstName,
            string? lastName,
            string? ageText,
            string? beltText,
            string? sexText,
            IEnumerable<string?> categoryValues,
            string? club)
        {
            if (string.IsNullOrWhiteSpace(firstName)
                || string.IsNullOrWhiteSpace(lastName)
                || string.IsNullOrWhiteSpace(ageText)
                || string.IsNullOrWhiteSpace(beltText)
                || string.IsNullOrWhiteSpace(sexText))
            {
                return null;
            }

            if (!int.TryParse(ageText, out var age) || age <= 0)
            {
                return null;
            }

            if (!Enum.TryParse(beltText, true, out Belts belt)
                || !Enum.TryParse(sexText, true, out Sex sex))
            {
                return null;
            }

            var categories = new List<CategoryType>();
            foreach (var categoryValue in categoryValues)
            {
                if (Enum.TryParse(categoryValue, true, out CategoryType categoryType))
                {
                    categories.Add(categoryType);
                }
            }

            if (categories.Count == 0)
            {
                return null;
            }

            return new Participant(firstName, lastName, age, belt, sex, categories, club);
        }
    }
}
