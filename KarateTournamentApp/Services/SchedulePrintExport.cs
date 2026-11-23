using System.Globalization;
using System.Net;
using System.Text;
using KarateClassLibrary;
using KarateClassLibrary.Contracts;

namespace KarateTournamentApp.Services;

internal static class SchedulePrintExport
{
    internal static string CreateHtml(TournamentScheduleDto schedule)
    {
        var html = new StringBuilder("""
            <!doctype html>
            <html lang="pl">
            <meta charset="utf-8">
            <title>Harmonogram turnieju</title>
            <style>
            @page { size: A4; margin: 16mm; }
            body { font: 11pt Arial, sans-serif; max-width: 54rem; margin: 1.5rem auto; }
            table { width: 100%; border-collapse: collapse; }
            th, td { border: 1px solid #666; padding: 0.45rem; text-align: left; }
            th:first-child, td:first-child { text-align: right; }
            thead { display: table-header-group; }
            tr { break-inside: avoid; }
            h2 { break-after: avoid; }
            @media print {
              body { margin: 0; max-width: none; }
              section + section { break-before: page; }
            }
            </style>
            <h1>Harmonogram turnieju</h1>
            """);

        foreach (var mat in schedule.Mats)
        {
            html.AppendLine($"<section><h2>Mata {mat.Number}</h2>");
            if (mat.Slots.Length == 0)
            {
                html.AppendLine("<p>Brak zaplanowanych konkurencji.</p></section>");
                continue;
            }

            html.AppendLine("<table><thead><tr><th>Lp.</th><th>Konkurencja</th><th>Dyscyplina</th><th>Od</th><th>Do</th></tr></thead><tbody>");
            for (var index = 0; index < mat.Slots.Length; index++)
            {
                var slot = mat.Slots[index];
                var name = WebUtility.HtmlEncode(slot.CategoryName);
                var discipline = slot.CategoryType == CategoryType.Team ? "Team Kata" : slot.CategoryType.ToString();
                html.AppendLine($"<tr><td>{index + 1}</td><td>{name}</td><td>{discipline}</td>"
                    + $"<td>{FormatTime(slot.StartsAt)}</td><td>{FormatTime(slot.EndsAt)}</td></tr>");
            }
            html.AppendLine("</tbody></table></section>");
        }

        if (schedule.Mats.Length == 0)
            html.AppendLine("<p>Brak zaplanowanych mat.</p>");
        return html.AppendLine("</html>").ToString();
    }

    private static string FormatTime(DateTime? time) => time?.ToString("dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture) ?? "—";
}
