namespace PawPoint.Services.Responses;

public class AssistantSuggestionResponse
{
    public string Key { get; set; } = string.Empty;
    public Dictionary<string, string> Params { get; set; } = new();
}