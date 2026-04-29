using StationApp.Application.DTOs;
using StationApp.Application.Interfaces;
using StationApp.Domain.Entities;

namespace StationApp.Application.UseCases;

public sealed class SearchUsersUseCase
{
    private readonly IUserRepository _userRepository;

    public SearchUsersUseCase(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<IReadOnlyList<UserListItemDto>> ExecuteAsync(SearchUsersRequest request, CancellationToken ct)
    {
        var users = await _userRepository.SearchAsync(
            request.Username?.Trim(),
            request.DisplayName?.Trim(),
            request.RoleCode?.Trim(),
            request.IsActive,
            ct);

        return users
            .Select(user => new UserListItemDto(
                user.Id,
                user.Username,
                user.DisplayName,
                user.RoleCode,
                user.IsActive,
                user.LastLoginAt,
                user.CreatedAt,
                user.CreatedBy,
                user.UpdatedAt,
                user.UpdatedBy))
            .ToList()
            .AsReadOnly();
    }
}

public sealed class CreateUserAccountUseCase
{
    private readonly IUserRepository _userRepository;
    private readonly IUserPasswordHasher _passwordHasher;
    private readonly ICurrentUserContext _currentUser;
    private readonly IClock _clock;
    private readonly IUnitOfWork _uow;
    private readonly IAuditService _auditService;

    public CreateUserAccountUseCase(
        IUserRepository userRepository,
        IUserPasswordHasher passwordHasher,
        ICurrentUserContext currentUser,
        IClock clock,
        IUnitOfWork uow,
        IAuditService auditService)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _currentUser = currentUser;
        _clock = clock;
        _uow = uow;
        _auditService = auditService;
    }

    public async Task<OperationResult<User>> ExecuteAsync(CreateUserAccountRequest request, CancellationToken ct)
    {
        var username = request.Username.Trim();
        var displayName = request.DisplayName.Trim();
        var roleCode = request.RoleCode.Trim();

        var validationError = ValidateCreateRequest(username, displayName, roleCode, request.Password, request.ConfirmPassword);
        if (validationError != null)
        {
            return OperationResult<User>.Fail(validationError);
        }

        if (await _userRepository.ExistsByUsernameAsync(username, ct))
        {
            return OperationResult<User>.Fail("Username đã tồn tại.");
        }

        var now = _clock.NowLocal;
        var actor = _currentUser.Username;
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = username,
            DisplayName = displayName,
            RoleCode = roleCode,
            PasswordHash = _passwordHasher.HashPassword(request.Password),
            IsActive = request.IsActive,
            LastLoginAt = null,
            CreatedAt = now,
            CreatedBy = actor,
            UpdatedAt = null,
            UpdatedBy = null
        };

        await _userRepository.AddAsync(user, ct);
        await _uow.SaveChangesAsync(ct);
        await _auditService.LogAsync("CREATE_USER_ACCOUNT", nameof(User), user.Id, new { user.Username, user.RoleCode }, ct);
        return OperationResult<User>.Ok(user);
    }

    private static string? ValidateCreateRequest(
        string username,
        string displayName,
        string roleCode,
        string password,
        string confirmPassword)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            return "Username là bắt buộc.";
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            return "Tên hiển thị là bắt buộc.";
        }

        if (string.IsNullOrWhiteSpace(roleCode))
        {
            return "Vai trò là bắt buộc.";
        }

        return ValidatePassword(password, confirmPassword);
    }

    internal static string? ValidatePassword(string password, string confirmPassword)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            return "Mật khẩu là bắt buộc.";
        }

        if (string.IsNullOrWhiteSpace(confirmPassword))
        {
            return "Xác nhận mật khẩu là bắt buộc.";
        }

        if (password.Length < 8)
        {
            return "Mật khẩu phải có ít nhất 8 ký tự.";
        }

        if (!string.Equals(password, confirmPassword, StringComparison.Ordinal))
        {
            return "Mật khẩu xác nhận không khớp.";
        }

        return null;
    }
}

public sealed class UpdateUserAccountUseCase
{
    private readonly IUserRepository _userRepository;
    private readonly ICurrentUserContext _currentUser;
    private readonly IClock _clock;
    private readonly IUnitOfWork _uow;
    private readonly IAuditService _auditService;

    public UpdateUserAccountUseCase(
        IUserRepository userRepository,
        ICurrentUserContext currentUser,
        IClock clock,
        IUnitOfWork uow,
        IAuditService auditService)
    {
        _userRepository = userRepository;
        _currentUser = currentUser;
        _clock = clock;
        _uow = uow;
        _auditService = auditService;
    }

    public async Task<OperationResult<User>> ExecuteAsync(UpdateUserAccountRequest request, CancellationToken ct)
    {
        var user = await _userRepository.GetByIdAsync(request.UserId, ct);
        if (user == null)
        {
            return OperationResult<User>.Fail("Không tìm thấy tài khoản.");
        }

        var displayName = request.DisplayName.Trim();
        var roleCode = request.RoleCode.Trim();
        if (string.IsNullOrWhiteSpace(displayName))
        {
            return OperationResult<User>.Fail("Tên hiển thị là bắt buộc.");
        }

        if (string.IsNullOrWhiteSpace(roleCode))
        {
            return OperationResult<User>.Fail("Vai trò là bắt buộc.");
        }

        user.DisplayName = displayName;
        user.RoleCode = roleCode;
        user.IsActive = request.IsActive;
        user.UpdatedAt = _clock.NowLocal;
        user.UpdatedBy = _currentUser.Username;

        await _userRepository.UpdateAsync(user, ct);
        await _uow.SaveChangesAsync(ct);
        await _auditService.LogAsync("UPDATE_USER_ACCOUNT", nameof(User), user.Id, new { user.Username, user.RoleCode, user.IsActive }, ct);
        return OperationResult<User>.Ok(user);
    }
}

public sealed class SetUserActiveStatusUseCase
{
    private readonly IUserRepository _userRepository;
    private readonly ICurrentUserContext _currentUser;
    private readonly IClock _clock;
    private readonly IUnitOfWork _uow;
    private readonly IAuditService _auditService;

    public SetUserActiveStatusUseCase(
        IUserRepository userRepository,
        ICurrentUserContext currentUser,
        IClock clock,
        IUnitOfWork uow,
        IAuditService auditService)
    {
        _userRepository = userRepository;
        _currentUser = currentUser;
        _clock = clock;
        _uow = uow;
        _auditService = auditService;
    }

    public async Task<OperationResult<User>> ExecuteAsync(SetUserActiveStatusRequest request, CancellationToken ct)
    {
        var user = await _userRepository.GetByIdAsync(request.UserId, ct);
        if (user == null)
        {
            return OperationResult<User>.Fail("Không tìm thấy tài khoản.");
        }

        user.IsActive = request.IsActive;
        user.UpdatedAt = _clock.NowLocal;
        user.UpdatedBy = _currentUser.Username;

        await _userRepository.UpdateAsync(user, ct);
        await _uow.SaveChangesAsync(ct);
        await _auditService.LogAsync(
            request.IsActive ? "REACTIVATE_USER_ACCOUNT" : "DEACTIVATE_USER_ACCOUNT",
            nameof(User),
            user.Id,
            new { user.Username, user.IsActive },
            ct);
        return OperationResult<User>.Ok(user);
    }
}

public sealed class ResetUserPasswordUseCase
{
    private readonly IUserRepository _userRepository;
    private readonly IUserPasswordHasher _passwordHasher;
    private readonly ICurrentUserContext _currentUser;
    private readonly IClock _clock;
    private readonly IUnitOfWork _uow;
    private readonly IAuditService _auditService;

    public ResetUserPasswordUseCase(
        IUserRepository userRepository,
        IUserPasswordHasher passwordHasher,
        ICurrentUserContext currentUser,
        IClock clock,
        IUnitOfWork uow,
        IAuditService auditService)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _currentUser = currentUser;
        _clock = clock;
        _uow = uow;
        _auditService = auditService;
    }

    public async Task<OperationResult<User>> ExecuteAsync(ResetUserPasswordRequest request, CancellationToken ct)
    {
        var user = await _userRepository.GetByIdAsync(request.UserId, ct);
        if (user == null)
        {
            return OperationResult<User>.Fail("Không tìm thấy tài khoản.");
        }

        var validationError = CreateUserAccountUseCase.ValidatePassword(request.NewPassword, request.ConfirmPassword);
        if (validationError != null)
        {
            return OperationResult<User>.Fail(validationError);
        }

        user.PasswordHash = _passwordHasher.HashPassword(request.NewPassword);
        user.UpdatedAt = _clock.NowLocal;
        user.UpdatedBy = _currentUser.Username;

        await _userRepository.UpdateAsync(user, ct);
        await _uow.SaveChangesAsync(ct);
        await _auditService.LogAsync("RESET_USER_PASSWORD", nameof(User), user.Id, new { user.Username }, ct);
        return OperationResult<User>.Ok(user);
    }
}
