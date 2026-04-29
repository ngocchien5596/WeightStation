using StationApp.Application.DTOs;
using StationApp.Application.Interfaces;

namespace StationApp.Application.UseCases;

public sealed class LoginUseCase
{
    private readonly IUserRepository _userRepository;
    private readonly IUserPasswordHasher _passwordHasher;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IClock _clock;
    private readonly IUnitOfWork _uow;

    public LoginUseCase(
        IUserRepository userRepository,
        IUserPasswordHasher passwordHasher,
        ICurrentUserContext currentUserContext,
        IClock clock,
        IUnitOfWork uow)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _currentUserContext = currentUserContext;
        _clock = clock;
        _uow = uow;
    }

    public async Task<OperationResult<CurrentUserSessionDto>> ExecuteAsync(LoginRequest request, CancellationToken ct)
    {
        var username = request.Username.Trim();
        if (string.IsNullOrWhiteSpace(username))
        {
            return OperationResult<CurrentUserSessionDto>.Fail("Vui lòng nhập Username.");
        }

        if (string.IsNullOrWhiteSpace(request.Password))
        {
            return OperationResult<CurrentUserSessionDto>.Fail("Vui lòng nhập Password.");
        }

        var user = await _userRepository.GetByUsernameAsync(username, ct);
        if (user == null)
        {
            return OperationResult<CurrentUserSessionDto>.Fail("Sai tài khoản hoặc mật khẩu.");
        }

        if (!user.IsActive)
        {
            return OperationResult<CurrentUserSessionDto>.Fail("Tài khoản đã ngừng hoạt động.");
        }

        if (string.IsNullOrWhiteSpace(user.PasswordHash))
        {
            return OperationResult<CurrentUserSessionDto>.Fail("Tài khoản chưa được cấu hình mật khẩu.");
        }

        if (!_passwordHasher.VerifyPassword(request.Password, user.PasswordHash))
        {
            return OperationResult<CurrentUserSessionDto>.Fail("Sai tài khoản hoặc mật khẩu.");
        }

        user.LastLoginAt = _clock.NowLocal;
        await _userRepository.UpdateAsync(user, ct);
        await _uow.SaveChangesAsync(ct);

        _currentUserContext.SignIn(user.Id, user.Username, user.DisplayName, user.RoleCode);

        return OperationResult<CurrentUserSessionDto>.Ok(new CurrentUserSessionDto(
            user.Id,
            user.Username,
            user.DisplayName,
            user.RoleCode,
            true));
    }
}
