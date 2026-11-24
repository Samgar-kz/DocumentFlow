using DocumentFlow.Domain.Common;

namespace DocumentFlow.Domain.Notifications;

public enum NotificationType
{
    ApprovalRequested = 0,
    DocumentApproved = 1,
    DocumentRejected = 2,
    CommentAdded = 3,
}

/// <summary>An in-app notification for one user.</summary>
public sealed class Notification : Entity
{
    private Notification()
    {
        // Required by EF Core.
    }

    public Notification(Guid userId, NotificationType type, Guid documentId, string message, DateTimeOffset createdAt)
    {
        UserId = userId;
        Type = type;
        DocumentId = documentId;
        Message = message.Length > 300 ? message[..300] : message;
        CreatedAt = createdAt;
    }

    public Guid UserId { get; private set; }

    public NotificationType Type { get; private set; }

    public Guid DocumentId { get; private set; }

    public string Message { get; private set; } = string.Empty;

    public bool IsRead { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public void MarkRead() => IsRead = true;
}
