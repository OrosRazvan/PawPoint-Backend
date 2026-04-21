namespace PawPoint.Services.Responses;

public class AssistantMessageResponse
{
    public string Intent { get; set; } = "Unknown";

    public string ReplyKey { get; set; } = string.Empty;
    public Dictionary<string, string> ReplyParams { get; set; } = new();

    public object? Data { get; set; }

    public List<AssistantSuggestionResponse> Suggestions { get; set; } = new();
}