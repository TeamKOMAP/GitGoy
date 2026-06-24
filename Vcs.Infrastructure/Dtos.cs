namespace Vcs.Infrastructure;

// Auth
public record RegisterReq(string Username, string Email, string Password);
public record LoginReq(string Username, string Password);
public record AuthRes(string Token, string Username, string Email);

// Projects
public record CreateProjectReq(string Name, string? Description, string Visibility = "public");
public record UpdateProjectReq(string? Name, string? Description, string? Visibility);
public record ProjectRes(Guid Id, string Name, string? Description, string Visibility, string DefaultBranch, string OwnerName, DateTime CreatedAtUtc);
public record AddMemberReq(string Username, string Role = "reader");
public record MemberRes(Guid UserId, string Username, string Role);

// Branches
public record BranchRes(string Name, bool IsDefault);
public record CreateBranchReq(string Name, string? SourceBranch);

// Commits
public record FileChange(string Path, string? Content, string Operation = "add");
public record CreateCommitReq(string Branch, string Message, List<FileChange> Changes);
public record CommitRes(string Sha, string Message, string Author, DateTime When);
public record CommitDetailRes(string Sha, string Message, string Author, DateTime When, List<FileChange> Changes);

// Files & Diff
public record FileEntryRes(string Name, string Path, bool IsDirectory);
public record DiffRes(string FilePath, string Patch, string Status);
