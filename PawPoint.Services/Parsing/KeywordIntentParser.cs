using System.Text.RegularExpressions;
using PawPoint.Services.Interfaces;

namespace PawPoint.Services.Parsing;

public class KeywordIntentParser : IIntentParser
{
    public ParsedAssistantIntent Parse(string message)
    {
        var normalized = Normalize(message);

        var result = new ParsedAssistantIntent
        {
            OriginalMessage = message,
            PetName = ExtractPetName(message),
            DaysAhead = ExtractDaysAhead(normalized)
        };

        if (ContainsAny(normalized, "salut", "buna", "bună", "hello", "hey"))
        {
            result.IntentType = AssistantIntentType.Greeting;
            return result;
        }

        if (ContainsAny(normalized, "ajutor", "help", "ce poti", "ce poți", "ce stii", "ce știi"))
        {
            result.IntentType = AssistantIntentType.Help;
            return result;
        }

        if (ContainsAny(normalized, "ce animale", "animalele mele", "lista animale", "animale am", "what pets"))
        {
            result.IntentType = AssistantIntentType.ListPets;
            return result;
        }

        if (ContainsAny(normalized, "programari", "programări", "consultatii", "consultații", "ce urmeaza", "ce urmează", "appointments"))
        {
            result.IntentType = AssistantIntentType.UpcomingAppointments;
            return result;
        }

        if (ContainsAny(normalized, "rezumat", "overview", "dashboard", "situatie generala", "situație generală", "summary"))
        {
            result.IntentType = AssistantIntentType.HealthOverview;
            return result;
        }

        if (ContainsAny(normalized, "sfat", "sfaturi", "recomand", "recomandare", "ce imi sugerezi", "ce îmi sugerezi", "recommend"))
        {
            result.IntentType = AssistantIntentType.Recommendations;
            return result;
        }

        if (ContainsAny(normalized, "vaccin", "vaccinare", "vaccination"))
        {
            result.IntentType = result.PetName is null
                ? AssistantIntentType.DueItems
                : AssistantIntentType.PetVaccinations;

            return result;
        }

        if (ContainsAny(normalized, "deparazit", "antiparazitar", "deworm"))
        {
            result.IntentType = result.PetName is null
                ? AssistantIntentType.DueItems
                : AssistantIntentType.PetDewormings;

            return result;
        }

        if (result.PetName is not null)
        {
            result.IntentType = AssistantIntentType.PetOverview;
            return result;
        }

        return result;
    }

    private static string Normalize(string input)
    {
        return input.Trim().ToLowerInvariant();
    }

    private static bool ContainsAny(string input, params string[] keywords)
    {
        return keywords.Any(input.Contains);
    }

    private static int ExtractDaysAhead(string input)
    {
        if (input.Contains("azi") || input.Contains("today")) return 1;
        if (input.Contains("maine") || input.Contains("mâine") || input.Contains("tomorrow")) return 2;
        if (input.Contains("saptamana") || input.Contains("săptămâna") || input.Contains("week")) return 7;
        if (input.Contains("luna") || input.Contains("month")) return 30;

        var match = Regex.Match(input, @"(\d+)\s*(zile|days)");
        if (match.Success && int.TryParse(match.Groups[1].Value, out var days))
            return days;

        return 30;
    }

    private static string? ExtractPetName(string message)
    {
        var patterns = new[]
        {
            @"pentru\s+([A-ZĂÂÎȘȚ][a-zA-ZăâîșțĂÂÎȘȚ\-]+)",
            @"despre\s+([A-ZĂÂÎȘȚ][a-zA-ZăâîșțĂÂÎȘȚ\-]+)",
            @"lui\s+([A-ZĂÂÎȘȚ][a-zA-ZăâîșțĂÂÎȘȚ\-]+)",
            @"pe\s+([A-ZĂÂÎȘȚ][a-zA-ZăâîșțĂÂÎȘȚ\-]+)",
            @"for\s+([A-Z][a-zA-Z\-]+)",
            @"about\s+([A-Z][a-zA-Z\-]+)"
        };

        foreach (var pattern in patterns)
        {
            var match = Regex.Match(message, pattern);
            if (match.Success)
                return match.Groups[1].Value.Trim();
        }

        return null;
    }
}