using System.Text.RegularExpressions;
using PawPoint.Services.Interfaces;

namespace PawPoint.Services.Parsing;

public class KeywordIntentParser : IIntentParser
{
    public ParsedAssistantIntent Parse(string message)
    {
        var normalized = Normalize(message);

        var intent = new ParsedAssistantIntent
        {
            OriginalMessage = message,
            DaysAhead = ExtractDaysAhead(normalized)
        };

        if (IsGreeting(normalized))
        {
            intent.IntentType = AssistantIntentType.Greeting;
            return intent;
        }

        if (ContainsAny(normalized, "ajutor", "help", "ce poti", "what can you"))
        {
            intent.IntentType = AssistantIntentType.Help;
            return intent;
        }

        if (ContainsAny(normalized,
            "recomanzi", "recomandare", "recomandari", "sfat", "sfaturi",
            "recommend", "recommendation", "recommendations", "advice", "suggest"))
        {
            intent.IntentType = AssistantIntentType.Recommendations;
            return intent;
        }

        if (ContainsAny(normalized,
            "ce animale am", "animalele mele", "lista animale", "pets", "my pets", "animals"))
        {
            intent.IntentType = AssistantIntentType.ListPets;
            return intent;
        }

        if (ContainsAny(normalized,
            "programari", "programare", "consultatie", "consultatii",
            "appointment", "appointments", "booking", "consultation"))
        {
            intent.IntentType = AssistantIntentType.UpcomingAppointments;
            return intent;
        }

        var asksVaccinations = ContainsAny(normalized,
            "vaccin", "vaccinare", "vaccinari", "vaccinuri",
            "vaccination", "vaccinations", "vaccine", "vaccines");

        var asksDewormings = ContainsAny(normalized,
            "deparazit", "deparazitare", "deparazitari",
            "deworm", "deworming", "dewormings");

        if (asksVaccinations && asksDewormings)
        {
            intent.IntentType = AssistantIntentType.DueItems;
            return intent;
        }

        if (asksVaccinations)
        {
            intent.IntentType = AssistantIntentType.DueVaccinations;
            return intent;
        }

        if (asksDewormings)
        {
            intent.IntentType = AssistantIntentType.DueDewormings;
            return intent;
        }

        if (ContainsAny(normalized,
            "rezumat", "overview", "summary", "general", "stare", "health"))
        {
            intent.IntentType = AssistantIntentType.HealthOverview;
            return intent;
        }

        intent.IntentType = AssistantIntentType.Unknown;
        return intent;
    }

    private static string Normalize(string value)
    {
        return value
            .Trim()
            .ToLowerInvariant()
            .Replace("ă", "a")
            .Replace("â", "a")
            .Replace("î", "i")
            .Replace("ș", "s")
            .Replace("ş", "s")
            .Replace("ț", "t")
            .Replace("ţ", "t");
    }

    private static bool ContainsAny(string text, params string[] keywords)
    {
        return keywords.Any(text.Contains);
    }

    private static bool IsGreeting(string text)
    {
        var words = text
            .Split(' ', '.', ',', '!', '?', ';', ':', '-', '_')
            .Where(w => !string.IsNullOrWhiteSpace(w));

        return words.Any(w =>
            w == "salut" ||
            w == "buna" ||
            w == "hello" ||
            w == "hi" ||
            w == "hey"
        );
    }

    private static int ExtractDaysAhead(string text)
    {
        if (text.Contains("saptamana") || text.Contains("week"))
            return 7;

        if (text.Contains("luna") || text.Contains("month"))
            return 30;

        return 30;
    }
}
