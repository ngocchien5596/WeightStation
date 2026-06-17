using StationApp.Application.DTOs;
using StationApp.Application.Interfaces;

namespace StationApp.Application.UseCases;

public sealed class LoginUseCase
{
    private readonly IUserRepository _userRepository;
    private readonly IUserPasswordHasher _passwordHasher;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly ICurrentStationContext _currentStationContext;
    private readonly IStationAuthorizationService _stationAuthorizationService;
    private readonly IClock _clock;
    private readonly IUnitOfWork _uow;

    public LoginUseCase(
        IUserRepository userRepository,
        IUserPasswordHasher passwordHasher,
        ICurrentUserContext currentUserContext,
        ICurrentStationContext currentStationContext,
        IStationAuthorizationService stationAuthorizationService,
        IClock clock,
        IUnitOfWork uow)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _currentUserContext = currentUserContext;
        _currentStationContext = currentStationContext;
        _stationAuthorizationService = stationAuthorizationService;
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

        var allowedStations = await _stationAuthorizationService.GetAllowedStationsAsync(user.Id, ct);
        if (allowedStations.Count == 0)
        {
            _currentUserContext.SignOut();
            _currentStationContext.Clear();
            return OperationResult<CurrentUserSessionDto>.Fail("Tài khoản chưa được phân quyền trạm cân. Vui lòng liên hệ quản trị viên.");
        }

        var selectedStation = allowedStations.FirstOrDefault(x => x.IsDefault) ?? allowedStations[0];
        _currentStationContext.SetStation(selectedStation.StationCode, selectedStation.StationName);

        return OperationResult<CurrentUserSessionDto>.Ok(new CurrentUserSessionDto(
            user.Id,
            user.Username,
            user.DisplayName,
            user.RoleCode,
            true));
    }
}
