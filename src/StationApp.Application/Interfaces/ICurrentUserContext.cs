namespace StationApp.Application.Interfaces;

public interface ICurrentUserContext
{
    Guid? UserId { get; }
    string Username { get; }
    string DisplayName { get; }
    string RoleCode { get; }
    bool IsAuthenticated { get; }

    void SignIn(Guid userId, string username, string displayName, string roleCode);
    void SignOut();
}
