using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using StationApp.Application.DTOs;
using StationApp.Application.Interfaces;
using StationApp.Application.UseCases;
using StationApp.UI.Services;
using StationApp.UI.ViewModels.Dialogs;

namespace StationApp.UI.ViewModels.Settings;

public partial class AccountManagementViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _scopeFactory;

    public ObservableCollection<string> RoleOptions { get; } = new(["ADMIN", "OPERATOR"]);
    public ObservableCollection<string> SearchRoleOptions { get; } = new(["Tất cả", "ADMIN", "OPERATOR"]);
    public ObservableCollection<string> StatusFilterOptions { get; } = new(["Tất cả", "Đang hoạt động", "Ngừng hoạt động"]);

    [ObservableProperty] private string _searchUsername = string.Empty;
    [ObservableProperty] private string _searchDisplayName = string.Empty;
    [ObservableProperty] private string _selectedSearchRoleOption = "Tất cả";
    [ObservableProperty] private string _selectedStatusFilter = "Tất cả";

    [ObservableProperty] private ObservableCollection<UserListItemDto> _users = new();
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ResetPasswordCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeactivateCommand))]
    [NotifyCanExecuteChangedFor(nameof(ReactivateCommand))]
    private UserListItemDto? _selectedUser;

    [ObservableProperty] private bool _isCreateMode = true;
    [ObservableProperty] private string _editUsername = string.Empty;
    [ObservableProperty] private string _editDisplayName = string.Empty;
    [ObservableProperty] private string _editRoleCode = string.Empty;
    [ObservableProperty] private string? _selectedRoleOption;
    [ObservableProperty] private bool _editIsActive = true;
    [ObservableProperty] private string _createPassword = string.Empty;
    [ObservableProperty] private string _createConfirmPassword = string.Empty;
    [ObservableProperty] private DateTime? _createdAt;
    [ObservableProperty] private string? _createdBy;
    [ObservableProperty] private DateTime? _updatedAt;
    [ObservableProperty] private string? _updatedBy;
    [ObservableProperty] private DateTime? _lastLoginAt;

    public bool IsEditMode => !IsCreateMode && SelectedUser != null;
    public bool IsUsernameReadOnly => !IsCreateMode;
    public bool IsPasswordSectionVisible => IsCreateMode;
    public bool CanResetPassword => SelectedUser != null;
    public bool CanDeactivate => SelectedUser?.IsActive == true;
    public bool CanReactivate => SelectedUser?.IsActive == false;

    public AccountManagementViewModel(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
        SelectedRoleOption = RoleOptions.FirstOrDefault();
    }

    partial void OnSelectedUserChanged(UserListItemDto? value)
    {
        if (value == null)
        {
            return;
        }

        IsCreateMode = false;
        EditUsername = value.Username;
        EditDisplayName = value.DisplayName;
        EditRoleCode = value.RoleCode;
        SelectedRoleOption = RoleOptions.Contains(value.RoleCode) ? value.RoleCode : RoleOptions.FirstOrDefault();
        EditIsActive = value.IsActive;
        CreatedAt = value.CreatedAt;
        CreatedBy = value.CreatedBy;
        UpdatedAt = value.UpdatedAt;
        UpdatedBy = value.UpdatedBy;
        LastLoginAt = value.LastLoginAt;
        CreatePassword = string.Empty;
        CreateConfirmPassword = string.Empty;
        RaiseModeStateChanged();
    }

    partial void OnIsCreateModeChanged(bool value)
    {
        RaiseModeStateChanged();
    }

    partial void OnSelectedRoleOptionChanged(string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            EditRoleCode = value;
        }
    }

    public async Task LoadAsync()
    {
        await SearchAsync();
    }

    [RelayCommand]
    private async Task SearchAsync()
    {
        await LoadUsersAsync(SelectedUser?.Id);
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        SearchUsername = string.Empty;
        SearchDisplayName = string.Empty;
        SelectedSearchRoleOption = SearchRoleOptions[0];
        SelectedStatusFilter = StatusFilterOptions[0];
        ResetToCreateMode();
        await LoadUsersAsync();
    }

    [RelayCommand]
    private void New()
    {
        ResetToCreateMode();
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var toast = scope.ServiceProvider.GetRequiredService<IToastService>();

        try
        {
            if (IsCreateMode)
            {
                var useCase = scope.ServiceProvider.GetRequiredService<CreateUserAccountUseCase>();
                var roleCode = ResolveRoleCodeForSave();
                var result = await useCase.ExecuteAsync(new CreateUserAccountRequest(
                    EditUsername,
                    EditDisplayName,
                    roleCode,
                    CreatePassword,
                    CreateConfirmPassword,
                    EditIsActive), CancellationToken.None);

                if (!result.Success || result.Data == null)
                {
                    HandleSaveFailure(toast, result.ErrorMessage);
                    return;
                }

                SearchUsername = string.Empty;
                SearchDisplayName = string.Empty;
                SelectedSearchRoleOption = SearchRoleOptions[0];
                SelectedStatusFilter = StatusFilterOptions[0];

                var persistedUser = await scope.ServiceProvider
                    .GetRequiredService<IUserRepository>()
                    .GetByUsernameAsync(result.Data.Username, CancellationToken.None);

                if (persistedUser == null)
                {
                    toast.ShowError("Không thể lưu tài khoản. Vui lòng thử lại.");
                    return;
                }

                toast.ShowSuccess("Đã tạo tài khoản thành công.");
                await LoadUsersAsync(persistedUser.Id);
                return;
            }

            if (SelectedUser == null)
            {
                toast.ShowWarning("Vui lòng chọn một tài khoản để cập nhật.");
                return;
            }

            var updateUseCase = scope.ServiceProvider.GetRequiredService<UpdateUserAccountUseCase>();
            var roleCodeForUpdate = ResolveRoleCodeForSave();
            var updateResult = await updateUseCase.ExecuteAsync(new UpdateUserAccountRequest(
                SelectedUser.Id,
                EditDisplayName,
                roleCodeForUpdate,
                EditIsActive), CancellationToken.None);

            if (!updateResult.Success || updateResult.Data == null)
            {
                HandleSaveFailure(toast, updateResult.ErrorMessage);
                return;
            }

            toast.ShowSuccess("Đã cập nhật tài khoản thành công.");
            await LoadUsersAsync(updateResult.Data.Id);
        }
        catch
        {
            toast.ShowError("Không thể lưu tài khoản. Vui lòng thử lại.");
        }
    }

    [RelayCommand(CanExecute = nameof(CanResetPassword))]
    private async Task ResetPasswordAsync()
    {
        if (SelectedUser == null)
        {
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var dialogService = scope.ServiceProvider.GetRequiredService<IDialogService>();
        var toast = scope.ServiceProvider.GetRequiredService<IToastService>();
        var dialogVm = new ResetPasswordDialogViewModel(SelectedUser.Username);
        var dialogResult = await dialogService.ShowCustomDialogAsync<ResetPasswordDialogViewModel, ResetPasswordDialogResult>(dialogVm);
        if (dialogResult == null)
        {
            return;
        }

        var useCase = scope.ServiceProvider.GetRequiredService<ResetUserPasswordUseCase>();
        var result = await useCase.ExecuteAsync(new ResetUserPasswordRequest(
            SelectedUser.Id,
            dialogResult.NewPassword,
            dialogResult.ConfirmPassword), CancellationToken.None);

        if (!result.Success || result.Data == null)
        {
            toast.ShowWarning(result.ErrorMessage ?? "Không thể lưu tài khoản. Vui lòng thử lại.");
            return;
        }

        toast.ShowSuccess("Đã reset mật khẩu thành công.");
        await LoadUsersAsync(SelectedUser.Id);
    }

    [RelayCommand(CanExecute = nameof(CanDeactivate))]
    private async Task DeactivateAsync()
    {
        await ChangeActiveStatusAsync(false);
    }

    [RelayCommand(CanExecute = nameof(CanReactivate))]
    private async Task ReactivateAsync()
    {
        await ChangeActiveStatusAsync(true);
    }

    private async Task ChangeActiveStatusAsync(bool isActive)
    {
        if (SelectedUser == null)
        {
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var dialogService = scope.ServiceProvider.GetRequiredService<IDialogService>();
        var toast = scope.ServiceProvider.GetRequiredService<IToastService>();
        var confirmed = await dialogService.ShowConfirmAsync(
            isActive ? "Xác nhận kích hoạt lại" : "Xác nhận ngừng hoạt động",
            isActive
                ? $"Bạn có chắc muốn kích hoạt lại tài khoản {SelectedUser.Username} không?"
                : $"Bạn có chắc muốn ngừng hoạt động tài khoản {SelectedUser.Username} không?",
            isActive ? "Kích hoạt lại" : "Ngừng hoạt động",
            "Không");

        if (!confirmed)
        {
            return;
        }

        var useCase = scope.ServiceProvider.GetRequiredService<SetUserActiveStatusUseCase>();
        var result = await useCase.ExecuteAsync(new SetUserActiveStatusRequest(SelectedUser.Id, isActive), CancellationToken.None);
        if (!result.Success || result.Data == null)
        {
            toast.ShowWarning(result.ErrorMessage ?? "Không thể lưu tài khoản. Vui lòng thử lại.");
            return;
        }

        toast.ShowSuccess(isActive
            ? "Đã kích hoạt lại tài khoản."
            : "Đã ngừng hoạt động tài khoản.");
        await LoadUsersAsync(SelectedUser.Id);
    }

    private async Task LoadUsersAsync(Guid? selectedId = null)
    {
        using var scope = _scopeFactory.CreateScope();
        var searchUseCase = scope.ServiceProvider.GetRequiredService<SearchUsersUseCase>();
        var users = await searchUseCase.ExecuteAsync(new SearchUsersRequest(
            SearchUsername,
            SearchDisplayName,
            ResolveSearchRoleFilter(),
            ResolveActiveFilter()), CancellationToken.None);

        Users = new ObservableCollection<UserListItemDto>(users);
        if (selectedId.HasValue)
        {
            SelectedUser = Users.FirstOrDefault(x => x.Id == selectedId.Value);
        }
        else if (Users.Count == 0)
        {
            SelectedUser = null;
        }

        if (SelectedUser == null && !IsCreateMode)
        {
            ResetToCreateMode();
        }
    }

    private bool? ResolveActiveFilter()
    {
        return SelectedStatusFilter switch
        {
            "Đang hoạt động" => true,
            "Ngừng hoạt động" => false,
            _ => null
        };
    }

    private string? ResolveSearchRoleFilter()
    {
        return string.Equals(SelectedSearchRoleOption, SearchRoleOptions[0], StringComparison.Ordinal)
            ? null
            : SelectedSearchRoleOption?.Trim();
    }

    private void HandleSaveFailure(IToastService toast, string? errorMessage)
    {
        if (string.Equals(errorMessage, "Username đã tồn tại.", StringComparison.Ordinal))
        {
            toast.ShowWarning("Username đã tồn tại.");
            return;
        }

        toast.ShowWarning(errorMessage ?? "Không thể lưu tài khoản. Vui lòng thử lại.");
    }

    private void ResetToCreateMode()
    {
        IsCreateMode = true;
        SelectedUser = null;
        EditUsername = string.Empty;
        EditDisplayName = string.Empty;
        SelectedRoleOption = RoleOptions.FirstOrDefault();
        EditRoleCode = SelectedRoleOption ?? string.Empty;
        EditIsActive = true;
        CreatePassword = string.Empty;
        CreateConfirmPassword = string.Empty;
        CreatedAt = null;
        CreatedBy = null;
        UpdatedAt = null;
        UpdatedBy = null;
        LastLoginAt = null;
        RaiseModeStateChanged();
    }

    private void RaiseModeStateChanged()
    {
        OnPropertyChanged(nameof(IsEditMode));
        OnPropertyChanged(nameof(IsUsernameReadOnly));
        OnPropertyChanged(nameof(IsPasswordSectionVisible));
        OnPropertyChanged(nameof(CanResetPassword));
        OnPropertyChanged(nameof(CanDeactivate));
        OnPropertyChanged(nameof(CanReactivate));
        ResetPasswordCommand.NotifyCanExecuteChanged();
        DeactivateCommand.NotifyCanExecuteChanged();
        ReactivateCommand.NotifyCanExecuteChanged();
    }

    private string ResolveRoleCodeForSave()
    {
        var roleCode = SelectedRoleOption?.Trim();
        if (!string.IsNullOrWhiteSpace(roleCode))
        {
            EditRoleCode = roleCode;
            return roleCode;
        }

        return EditRoleCode.Trim();
    }

    private void EnsureRoleOptionExists(string roleCode)
    {
        if (string.IsNullOrWhiteSpace(roleCode) || RoleOptions.Contains(roleCode))
        {
            return;
        }
    }
}
