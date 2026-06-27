namespace Vcs.Domain.Enums;

public enum AuditActionType
{
    ProjectCreated = 1,
    ProjectUpdated = 2,
    MemberAdded = 3,
    BranchCreated = 4,
    CommitCreated = 5,
    UserSubscribed = 6,
    UserUnsubscribed = 7
}