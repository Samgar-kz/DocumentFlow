using DocumentFlow.Application.Abstractions;
using DocumentFlow.Domain.Audit;
using DocumentFlow.Domain.Common;
using DocumentFlow.Domain.Documents;
using DocumentFlow.Domain.Notifications;

namespace DocumentFlow.Application.Events;

/// <summary>Writes an audit entry for every business-relevant document event.</summary>
public sealed class AuditTrailHandler : IDomainEventHandler
{
    public void Handle(IDomainEvent domainEvent, IAppDbContext db)
    {
        if (domainEvent is not DocumentEvent e)
        {
            return;
        }

        (AuditAction Action, string Details)? entry = e switch
        {
            DocumentCreated => (AuditAction.DocumentCreated, "Created the document"),
            DocumentDetailsUpdated => (AuditAction.DetailsUpdated, "Updated the title, description or category"),
            VersionUploaded v => (AuditAction.VersionUploaded, $"Uploaded version {v.VersionNumber} ({v.FileName})"),
            DocumentSubmitted s => (AuditAction.Submitted, $"Submitted for approval to {s.ApproverIds.Count} approver(s)"),
            StepApproved a => (AuditAction.StepApproved, a.Comment is null ? $"Approved step {a.StepOrder}" : $"Approved step {a.StepOrder}: {a.Comment}"),
            DocumentApproved => (AuditAction.Approved, "All approvers signed off; the document is approved"),
            DocumentRejected r => (AuditAction.Rejected, $"Rejected: {r.Comment}"),
            DocumentWithdrawn => (AuditAction.Withdrawn, "Withdrew the document from review"),
            CommentAdded => (AuditAction.CommentAdded, "Added a comment"),
            DocumentDeleted => (AuditAction.Deleted, "Deleted the draft"),
            _ => null,
        };

        if (entry is { } value)
        {
            db.AuditEntries.Add(new AuditEntry(e.ActorId, value.Action, e.OccurredAt, e.DocumentId, e.DocumentTitle, value.Details));
        }
    }
}

/// <summary>Notifies the people who need to act on, or know about, a change.</summary>
public sealed class NotificationHandler : IDomainEventHandler
{
    public void Handle(IDomainEvent domainEvent, IAppDbContext db)
    {
        switch (domainEvent)
        {
            case ApprovalRequested e:
                Notify(db, e.ApproverId, NotificationType.ApprovalRequested, e, $"\"{e.DocumentTitle}\" is waiting for your approval.");
                break;
            case DocumentApproved e:
                Notify(db, e.OwnerId, NotificationType.DocumentApproved, e, $"\"{e.DocumentTitle}\" has been approved.");
                break;
            case DocumentRejected e:
                Notify(db, e.OwnerId, NotificationType.DocumentRejected, e, $"\"{e.DocumentTitle}\" was rejected: {e.Comment}");
                break;
            case CommentAdded e:
                foreach (var recipient in e.RecipientIds)
                {
                    Notify(db, recipient, NotificationType.CommentAdded, e, $"New comment on \"{e.DocumentTitle}\".");
                }

                break;
        }
    }

    private static void Notify(IAppDbContext db, Guid userId, NotificationType type, DocumentEvent e, string message)
    {
        if (userId != e.ActorId)
        {
            db.Notifications.Add(new Notification(userId, type, e.DocumentId, message, e.OccurredAt));
        }
    }
}
