using PawPoint.DB.Entities;

public class AssistantConversation
{
    public int Id { get; set; }

    public int UserId { get; set; }
    public User User { get; set; }

    public string Title { get; set; } = "New Chat";

    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    public bool IsDeleted { get; set; }

    public ICollection<AssistantMessage> Messages { get; set; }
        = new List<AssistantMessage>();
}