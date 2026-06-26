namespace StationApp.Application.Interfaces;

public interface ICurrentUserContext
{
    Guid? UserId { get; }
    string Username { get; }
    string DisplayName { get; }
    string RoleCode { get; }
    string StationCode { get; }
    bool IsAuthenticated { get; }

    void SignIn(Guid userId, string username, string displayName, string roleCode, string stationCode);
    void UpdateStationCode(string stationCode);
    void SignOut();
}
