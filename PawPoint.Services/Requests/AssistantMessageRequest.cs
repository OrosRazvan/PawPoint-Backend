namespace PawPoint.Services.Requests;

public class AssistantMessageRequest
{
    public string Message { get; set; } = string.Empty;
    public int UserId { get; set; }
}