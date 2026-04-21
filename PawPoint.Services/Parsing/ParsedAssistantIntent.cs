namespace PawPoint.Services.Parsing;

public class ParsedAssistantIntent
{
    public AssistantIntentType IntentType { get; set; } = AssistantIntentType.Unknown;
    public string OriginalMessage { get; set; } = string.Empty;
    public string? PetName { get; set; }
    public int DaysAhead { get; set; } = 30;
}