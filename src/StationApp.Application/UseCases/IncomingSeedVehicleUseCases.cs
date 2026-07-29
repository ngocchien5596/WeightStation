using StationApp.Application.DTOs;
using StationApp.Application.Interfaces;
using StationApp.Application.Services;
using StationApp.Domain.Constants;
using StationApp.Domain.Entities;
using StationApp.Domain.Enums;

namespace StationApp.Application.UseCases;

public static class IncomingSeedVehicleRules
{
    public const string StationCode = "QN01";
    public const string TransactionType = "INBOUND";
}

public sealed class GetIncomingSeedVehiclesUseCase
{
    private readonly IIncomingSeedVehicleRepository _repo;

    public GetIncomingSeedVehiclesUseCase(IIncomingSeedVehicleRepository repo)
    {
        _repo = repo;
    }

    public Task<IReadOnlyList<IncomingSeedVehicleListItem>> ExecuteAsync(CancellationToken ct)
        => _repo.GetQn01Async(ct);
}

public sealed class CreateIncomingSeedVehicleUseCase
{
    private readonly IIncomingSeedVehicleRepository _repo;
    private readonly ICustomerRepository _customerRepo;
    private readonly IProductRepository _productRepo;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserContext _userContext;
    private readonly IClock _clock;
    private readonly IAuditService _audit;
    private readonly ISyncOutboxRepository _syncOutboxRepo;
    private readonly ISyncPayloadFactory _syncPayloadFactory;

    public CreateIncomingSeedVehicleUseCase(
        IIncomingSeedVehicleRepository repo,
        ICustomerRepository customerRepo,
        IProductRepository productRepo,
        IUnitOfWork uow,
        ICurrentUserContext userContext,
        IClock clock,
        IAuditService audit,
        ISyncOutboxRepository syncOutboxRepo,
        ISyncPayloadFactory syncPayloadFactory)
    {
        _repo = repo;
        _customerRepo = customerRepo;
        _productRepo = productRepo;
        _uow = uow;
        _userContext = userContext;
        _clock = clock;
        _audit = audit;
        _syncOutboxRepo = syncOutboxRepo;
        _syncPayloadFactory = syncPayloadFactory;
    }

    public async Task<OperationResult<IncomingSeedVehicle>> ExecuteAsync(CreateIncomingSeedVehicleRequest request, CancellationToken ct)
    {
        var validation = await IncomingSeedVehicleValidation.ResolveMasterDataAsync(_customerRepo, _productRepo, request.CustomerCode, request.ProductCode, ct);
        if (!validation.Success)
        {
            return OperationResult<IncomingSeedVehicle>.Fail(validation.ErrorMessage ?? "Thông tin xe nhập mẫu không hợp lệ.");
        }

        var duplicate = await _repo.FindActiveDuplicateQn01Async(validation.Customer!.CustomerCode, validation.Product!.ProductCode, null, ct);
        if (duplicate != null)
        {
            return OperationResult<IncomingSeedVehicle>.Fail("Xe nhập mẫu với Khách hàng và Sản phẩm này đã tồn tại.");
        }

        var sortOrder = request.SortOrder.GetValueOrDefault();
        if (sortOrder <= 0)
        {
            sortOrder = await _repo.GetNextSortOrderQn01Async(ct);
        }

        var now = _clock.NowLocal;
        var seed = new IncomingSeedVehicle
        {
            Id = Guid.NewGuid(),
            StationCode = IncomingSeedVehicleRules.StationCode,
            TransactionType = IncomingSeedVehicleRules.TransactionType,
            CustomerCode = validation.Customer.CustomerCode,
            CustomerName = validation.Customer.CustomerName,
            ProductCode = validation.Product.ProductCode,
            ProductName = validation.Product.ProductName,
            ProductType = ProductTypes.Normalize(validation.Product.ProductType),
            SortOrder = sortOrder,
            IsActive = request.IsActive,
            CreatedAt = now,
            CreatedBy = _userContext.Username
        };

        await _uow.ExecuteInTransactionAsync(async innerCt =>
        {
            await _repo.AddAsync(seed, innerCt);
            await IncomingSeedVehicleSync.EnqueueAsync(_syncOutboxRepo, _syncPayloadFactory, seed, now, innerCt);
        }, ct);

        await _audit.LogAsync(
            "CREATE_INCOMING_SEED_VEHICLE",
            nameof(IncomingSeedVehicle),
            seed.Id,
            new AuditLogDetailBuilder()
                .WithSubject("Name", $"{seed.CustomerName} - {seed.ProductName}")
                .WithSubject(nameof(IncomingSeedVehicle.StationCode), seed.StationCode)
                .AddChange(nameof(IncomingSeedVehicle.CustomerName), null, seed.CustomerName)
                .AddChange(nameof(IncomingSeedVehicle.ProductName), null, seed.ProductName)
                .AddChange(nameof(IncomingSeedVehicle.SortOrder), null, seed.SortOrder)
                .AddChange(nameof(IncomingSeedVehicle.IsActive), null, seed.IsActive)
                .Build(),
            ct);

        return OperationResult<IncomingSeedVehicle>.Ok(seed);
    }
}

public sealed class UpdateIncomingSeedVehicleUseCase
{
    private readonly IIncomingSeedVehicleRepository _repo;
    private readonly ICustomerRepository _customerRepo;
    private readonly IProductRepository _productRepo;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserContext _userContext;
    private readonly IClock _clock;
    private readonly IAuditService _audit;
    private readonly ISyncOutboxRepository _syncOutboxRepo;
    private readonly ISyncPayloadFactory _syncPayloadFactory;

    public UpdateIncomingSeedVehicleUseCase(
        IIncomingSeedVehicleRepository repo,
        ICustomerRepository customerRepo,
        IProductRepository productRepo,
        IUnitOfWork uow,
        ICurrentUserContext userContext,
        IClock clock,
        IAuditService audit,
        ISyncOutboxRepository syncOutboxRepo,
        ISyncPayloadFactory syncPayloadFactory)
    {
        _repo = repo;
        _customerRepo = customerRepo;
        _productRepo = productRepo;
        _uow = uow;
        _userContext = userContext;
        _clock = clock;
        _audit = audit;
        _syncOutboxRepo = syncOutboxRepo;
        _syncPayloadFactory = syncPayloadFactory;
    }

    public async Task<OperationResult<IncomingSeedVehicle>> ExecuteAsync(UpdateIncomingSeedVehicleRequest request, CancellationToken ct)
    {
        var seed = await _repo.GetByIdAsync(request.Id, ct);
        if (seed == null)
        {
            return OperationResult<IncomingSeedVehicle>.Fail("Không tìm thấy xe nhập mẫu cần cập nhật.");
        }

        var validation = await IncomingSeedVehicleValidation.ResolveMasterDataAsync(_customerRepo, _productRepo, request.CustomerCode, request.ProductCode, ct);
        if (!validation.Success)
        {
            return OperationResult<IncomingSeedVehicle>.Fail(validation.ErrorMessage ?? "Thông tin xe nhập mẫu không hợp lệ.");
        }

        var duplicate = await _repo.FindActiveDuplicateQn01Async(validation.Customer!.CustomerCode, validation.Product!.ProductCode, seed.Id, ct);
        if (duplicate != null)
        {
            return OperationResult<IncomingSeedVehicle>.Fail("Xe nhập mẫu với Khách hàng và Sản phẩm này đã tồn tại.");
        }

        var oldCustomerName = seed.CustomerName;
        var oldProductName = seed.ProductName;
        var oldSortOrder = seed.SortOrder;
        var oldIsActive = seed.IsActive;

        seed.CustomerCode = validation.Customer.CustomerCode;
        seed.CustomerName = validation.Customer.CustomerName;
        seed.ProductCode = validation.Product.ProductCode;
        seed.ProductName = validation.Product.ProductName;
        seed.ProductType = ProductTypes.Normalize(validation.Product.ProductType);
        seed.SortOrder = request.SortOrder <= 0 ? oldSortOrder : request.SortOrder;
        seed.IsActive = request.IsActive;
        seed.UpdatedAt = _clock.NowLocal;
        seed.UpdatedBy = _userContext.Username;

        await _uow.ExecuteInTransactionAsync(async innerCt =>
        {
            await _repo.UpdateAsync(seed, innerCt);
            await IncomingSeedVehicleSync.EnqueueAsync(_syncOutboxRepo, _syncPayloadFactory, seed, seed.UpdatedAt ?? _clock.NowLocal, innerCt);
        }, ct);

        await _audit.LogAsync(
            "UPDATE_INCOMING_SEED_VEHICLE",
            nameof(IncomingSeedVehicle),
            seed.Id,
            new AuditLogDetailBuilder()
                .WithSubject("Name", $"{seed.CustomerName} - {seed.ProductName}")
                .WithSubject(nameof(IncomingSeedVehicle.StationCode), seed.StationCode)
                .AddChange(nameof(IncomingSeedVehicle.CustomerName), oldCustomerName, seed.CustomerName)
                .AddChange(nameof(IncomingSeedVehicle.ProductName), oldProductName, seed.ProductName)
                .AddChange(nameof(IncomingSeedVehicle.SortOrder), oldSortOrder, seed.SortOrder)
                .AddChange(nameof(IncomingSeedVehicle.IsActive), oldIsActive, seed.IsActive)
                .Build(),
            ct);

        return OperationResult<IncomingSeedVehicle>.Ok(seed);
    }
}

public sealed class DeleteIncomingSeedVehicleUseCase
{
    private readonly IIncomingSeedVehicleRepository _repo;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserContext _userContext;
    private readonly IClock _clock;
    private readonly IAuditService _audit;
    private readonly ISyncOutboxRepository _syncOutboxRepo;
    private readonly ISyncPayloadFactory _syncPayloadFactory;

    public DeleteIncomingSeedVehicleUseCase(
        IIncomingSeedVehicleRepository repo,
        IUnitOfWork uow,
        ICurrentUserContext userContext,
        IClock clock,
        IAuditService audit,
        ISyncOutboxRepository syncOutboxRepo,
        ISyncPayloadFactory syncPayloadFactory)
    {
        _repo = repo;
        _uow = uow;
        _userContext = userContext;
        _clock = clock;
        _audit = audit;
        _syncOutboxRepo = syncOutboxRepo;
        _syncPayloadFactory = syncPayloadFactory;
    }

    public async Task<OperationResult<bool>> ExecuteAsync(Guid id, CancellationToken ct)
    {
        var seed = await _repo.GetByIdAsync(id, ct);
        if (seed == null || !seed.IsActive)
        {
            return OperationResult<bool>.Fail("Không tìm thấy xe nhập mẫu cần xóa.");
        }

        var oldCustomerName = seed.CustomerName;
        var oldProductName = seed.ProductName;

        seed.IsActive = false;
        seed.UpdatedAt = _clock.NowLocal;
        seed.UpdatedBy = _userContext.Username;
        seed.DeletedAt = seed.UpdatedAt;
        seed.DeletedBy = _userContext.Username;

        await _uow.ExecuteInTransactionAsync(async innerCt =>
        {
            await _repo.UpdateAsync(seed, innerCt);
            await IncomingSeedVehicleSync.EnqueueAsync(_syncOutboxRepo, _syncPayloadFactory, seed, seed.UpdatedAt ?? _clock.NowLocal, innerCt);
        }, ct);

        await _audit.LogAsync(
            "DELETE_INCOMING_SEED_VEHICLE",
            nameof(IncomingSeedVehicle),
            seed.Id,
            new AuditLogDetailBuilder()
                .WithSubject("Name", $"{oldCustomerName} - {oldProductName}")
                .WithSubject(nameof(IncomingSeedVehicle.StationCode), seed.StationCode)
                .AddChange(nameof(IncomingSeedVehicle.IsActive), true, false)
                .WithSummary(nameof(IncomingSeedVehicle.CustomerName), oldCustomerName)
                .WithSummary(nameof(IncomingSeedVehicle.ProductName), oldProductName)
                .Build(),
            ct);

        return OperationResult<bool>.Ok(true);
    }
}

internal static class IncomingSeedVehicleValidation
{
    public sealed record Result(bool Success, Customer? Customer, Product? Product, string? ErrorMessage);

    public static async Task<Result> ResolveMasterDataAsync(
        ICustomerRepository customerRepo,
        IProductRepository productRepo,
        string? customerCode,
        string? productCode,
        CancellationToken ct)
    {
        var normalizedCustomerCode = customerCode?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedCustomerCode))
        {
            return new Result(false, null, null, "Vui lòng chọn Khách hàng.");
        }

        var normalizedProductCode = productCode?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedProductCode))
        {
            return new Result(false, null, null, "Vui lòng chọn Sản phẩm.");
        }

        var customer = await customerRepo.GetByCodeAsync(normalizedCustomerCode, ct);
        if (customer == null || !customer.IsActive)
        {
            return new Result(false, null, null, "Khách hàng không tồn tại hoặc đã ngừng hoạt động.");
        }

        if (!CustomerBusinessRoles.AllowsTransaction(customer.CustomerBusinessRole, TransactionType.INBOUND))
        {
            return new Result(false, null, null, "Khách hàng không thuộc luồng Nhập hàng.");
        }

        var product = await productRepo.GetByCodeAsync(normalizedProductCode, ct);
        if (product == null || !product.IsActive)
        {
            return new Result(false, null, null, "Sản phẩm không tồn tại hoặc đã ngừng hoạt động.");
        }

        if (!ProductTransactionScopes.AllowsTransaction(product.TransactionScope, TransactionType.INBOUND))
        {
            return new Result(false, null, null, "Sản phẩm không thuộc luồng Nhập hàng.");
        }

        if (!string.Equals(ProductTypes.Normalize(product.ProductType), ProductTypes.Inbound, StringComparison.OrdinalIgnoreCase))
        {
            return new Result(false, null, null, "S\u1ea3n ph\u1ea9m ph\u1ea3i c\u00f3 Lo\u1ea1i s\u1ea3n ph\u1ea9m l\u00e0 H\u00e0ng nh\u1eadp.");
        }

        return new Result(true, customer, product, null);
    }
}

internal static class IncomingSeedVehicleSync
{
    public static async Task EnqueueAsync(
        ISyncOutboxRepository outboxRepo,
        ISyncPayloadFactory payloadFactory,
        IncomingSeedVehicle seed,
        DateTime now,
        CancellationToken ct)
    {
        await outboxRepo.EnqueueAsync(new SyncOutbox
        {
            Id = Guid.NewGuid(),
            AggregateId = seed.Id,
            AggregateType = SyncAggregateTypes.IncomingSeedVehicle,
            PayloadJson = payloadFactory.CreatePayload(seed),
            IdempotencyKey = seed.Id,
            Status = OutboxStatus.PENDING,
            RetryCount = 0,
            CreatedAt = now,
            UpdatedAt = now
        }, ct);
    }
}
