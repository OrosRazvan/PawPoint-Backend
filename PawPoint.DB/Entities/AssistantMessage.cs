public class AssistantMessage
{
    public int Id { get; set; }

    public int ConversationId { get; set; }
    public AssistantConversation Conversation { get; set; }

    public string Role { get; set; } = string.Empty;
    // user / assistant

    public string Content { get; set; } = string.Empty;

    public string? Intent { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}