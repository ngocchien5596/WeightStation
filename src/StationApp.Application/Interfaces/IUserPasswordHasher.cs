namespace StationApp.Application.Interfaces;

public interface IUserPasswordHasher
{
    string HashPassword(string password);
    bool VerifyPassword(string password, string passwordHash);
}
