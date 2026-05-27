USE [StationAppLocal]
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

IF OBJECT_ID(N'dbo.sp_UpsertCutOrderFromErp', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE [dbo].[sp_UpsertCutOrderFromErp] AS BEGIN SET NOCOUNT ON; END');
GO

ALTER PROCEDURE [dbo].[sp_UpsertCutOrderFromErp]
    @ErpCutOrderId NVARCHAR(100),
    @ErpRegistrationCode NVARCHAR(100) = NULL,
    @CutOrderSource NVARCHAR(40) = NULL,
    @CutOrderStatus NVARCHAR(60) = NULL,
    @TransactionType NVARCHAR(20),
    @TransportMethod NVARCHAR(20) = NULL,
    @VehiclePlate NVARCHAR(30),
    @MoocNumber NVARCHAR(30) = NULL,
    @ReceiverName NVARCHAR(100) = NULL,
    @CustomerCode NVARCHAR(50),
    @CustomerName NVARCHAR(255) = NULL,
    @Market NVARCHAR(255) = NULL,
    @ConsumptionPlace NVARCHAR(255) = NULL,
    @ProductCode NVARCHAR(50),
    @ProductName NVARCHAR(255) = NULL,
    @PlannedWeight DECIMAL(18,3) = NULL,
    @BagCount DECIMAL(18,3) = NULL,
    @ProductType NVARCHAR(30) = NULL,
    @ProcessingStage NVARCHAR(60) = NULL,
    @IsCancelled BIT = 0,
    @HasOverweightCase BIT = 0,
    @SyncStatus NVARCHAR(40) = NULL,
    @IdempotencyKey UNIQUEIDENTIFIER = NULL,
    @IsInboundProcessed BIT = 0,
    @CreatedAt DATETIME2(7) = NULL,
    @CreatedBy NVARCHAR(200) = NULL,
    @UpdatedAt DATETIME2(7) = NULL,
    @UpdatedBy NVARCHAR(200) = NULL,
    @ReceiverIdNo NVARCHAR(50) = NULL,
    @OrderCode NVARCHAR(100) = NULL,
    @LotNo NVARCHAR(100) = NULL,
    @RepresentativeName NVARCHAR(150) = NULL,
    @LoadingPlace NVARCHAR(255) = NULL,
    @SealNo NVARCHAR(100) = NULL,
    @Notes NVARCHAR(500) = NULL,
    @CreatedAtUtc DATETIME2(7) = NULL,
    @CutOrderCode NVARCHAR(100) = NULL,
    @DriverName NVARCHAR(100) = NULL,
    @TtcpWeight DECIMAL(18,3) = NULL,
    @VehicleRegistrationNo NVARCHAR(50) = NULL,
    @VehicleRegistrationExpiryDate DATETIME2(7) = NULL,
    @MoocRegistrationNo NVARCHAR(50) = NULL,
    @MoocRegistrationExpiryDate DATETIME2(7) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @NowUtc DATETIME2(7) = COALESCE(@UpdatedAt, @CreatedAt, @CreatedAtUtc, SYSUTCDATETIME());
    DECLARE @CreateUtc DATETIME2(7) = COALESCE(@CreatedAt, @CreatedAtUtc, @NowUtc);
    DECLARE @SystemUser NVARCHAR(200) = COALESCE(NULLIF(LTRIM(RTRIM(@UpdatedBy)), N''), NULLIF(LTRIM(RTRIM(@CreatedBy)), N''), N'ERP_SQL_PROC');
    DECLARE @CutOrderId UNIQUEIDENTIFIER;
    DECLARE @ExistingProcessingStage NVARCHAR(60);
    DECLARE @ExistingStatus NVARCHAR(60);
    DECLARE @ExistingSessionId UNIQUEIDENTIFIER;
    DECLARE @ExistingIsCancelled BIT;
    DECLARE @NormalizedProductType NVARCHAR(30);
    DECLARE @CarryForwardNotes NVARCHAR(500);

    SET @ErpCutOrderId = NULLIF(LTRIM(RTRIM(@ErpCutOrderId)), N'');
    SET @ErpRegistrationCode = NULLIF(LTRIM(RTRIM(@ErpRegistrationCode)), N'');
    SET @CutOrderSource = UPPER(NULLIF(LTRIM(RTRIM(@CutOrderSource)), N''));
    SET @CutOrderStatus = UPPER(NULLIF(LTRIM(RTRIM(@CutOrderStatus)), N''));
    SET @TransactionType = UPPER(NULLIF(LTRIM(RTRIM(@TransactionType)), N''));
    SET @TransportMethod = UPPER(NULLIF(LTRIM(RTRIM(@TransportMethod)), N''));
    SET @VehiclePlate = UPPER(NULLIF(LTRIM(RTRIM(@VehiclePlate)), N''));
    SET @MoocNumber = NULLIF(LTRIM(RTRIM(@MoocNumber)), N'');
    SET @ReceiverName = NULLIF(LTRIM(RTRIM(@ReceiverName)), N'');
    SET @CustomerCode = NULLIF(LTRIM(RTRIM(@CustomerCode)), N'');
    SET @CustomerName = NULLIF(LTRIM(RTRIM(@CustomerName)), N'');
    SET @Market = NULLIF(LTRIM(RTRIM(@Market)), N'');
    SET @ConsumptionPlace = NULLIF(LTRIM(RTRIM(@ConsumptionPlace)), N'');
    SET @ProductCode = NULLIF(LTRIM(RTRIM(@ProductCode)), N'');
    SET @ProductName = NULLIF(LTRIM(RTRIM(@ProductName)), N'');
    SET @ProductType = NULLIF(LTRIM(RTRIM(@ProductType)), N'');
    SET @ProcessingStage = UPPER(NULLIF(LTRIM(RTRIM(@ProcessingStage)), N''));
    SET @SyncStatus = UPPER(NULLIF(LTRIM(RTRIM(@SyncStatus)), N''));
    SET @ReceiverIdNo = NULLIF(LTRIM(RTRIM(@ReceiverIdNo)), N'');
    SET @OrderCode = NULLIF(LTRIM(RTRIM(@OrderCode)), N'');
    SET @LotNo = NULLIF(LTRIM(RTRIM(@LotNo)), N'');
    SET @RepresentativeName = NULLIF(LTRIM(RTRIM(@RepresentativeName)), N'');
    SET @LoadingPlace = NULLIF(LTRIM(RTRIM(@LoadingPlace)), N'');
    SET @SealNo = NULLIF(LTRIM(RTRIM(@SealNo)), N'');
    SET @Notes = NULLIF(LTRIM(RTRIM(@Notes)), N'');
    SET @CutOrderCode = NULLIF(LTRIM(RTRIM(@CutOrderCode)), N'');
    SET @DriverName = NULLIF(LTRIM(RTRIM(@DriverName)), N'');
    SET @VehicleRegistrationNo = NULLIF(LTRIM(RTRIM(@VehicleRegistrationNo)), N'');
    SET @MoocRegistrationNo = NULLIF(LTRIM(RTRIM(@MoocRegistrationNo)), N'');

    IF @ReceiverName IS NULL AND @DriverName IS NOT NULL
        SET @ReceiverName = @DriverName;

    SET @NormalizedProductType = @ProductType;
    IF @NormalizedProductType = N'Bao'
        SET @ProductType = N'Bao';
    ELSE IF @NormalizedProductType = N'Roi/Xa' OR @NormalizedProductType LIKE N'R_i/X_'
        SET @ProductType = N'Roi/Xa';
    ELSE IF @NormalizedProductType = N'Xa dong bao' OR @NormalizedProductType LIKE N'X_ ____ bao'
        SET @ProductType = N'Bao';

    IF @ErpCutOrderId IS NULL
        THROW 51001, N'ErpCutOrderId la bat buoc.', 1;

    IF @VehiclePlate IS NULL
        THROW 51002, N'VehiclePlate la bat buoc.', 1;

    IF @CustomerCode IS NULL
        THROW 51003, N'CustomerCode la bat buoc.', 1;

    IF @ProductCode IS NULL
        THROW 51004, N'ProductCode la bat buoc.', 1;

    IF @TransactionType NOT IN (N'OUTBOUND', N'INBOUND')
        THROW 51005, N'TransactionType chi nhan OUTBOUND hoac INBOUND.', 1;

    IF @TransportMethod IS NOT NULL AND @TransportMethod NOT IN (N'ROAD', N'WATERWAY')
        THROW 51006, N'TransportMethod chi nhan ROAD hoac WATERWAY.', 1;

    IF @ProductType IS NOT NULL AND @ProductType NOT IN (N'Bao', N'Roi/Xa')
        THROW 51007, N'ProductType khong hop le.', 1;

    IF @PlannedWeight IS NOT NULL AND @PlannedWeight < 0
        THROW 51008, N'PlannedWeight khong duoc am.', 1;

    IF @BagCount IS NOT NULL AND @BagCount < 0
        THROW 51009, N'BagCount khong duoc am.', 1;

    IF @ProductType = N'Bao' AND @BagCount IS NULL
        THROW 51010, N'ProductType = Bao yeu cau BagCount.', 1;

    IF @ErpRegistrationCode IS NULL
        SET @ErpRegistrationCode = @ErpCutOrderId;

    IF @CutOrderSource IS NULL
        SET @CutOrderSource = N'ERP';

    IF @CutOrderStatus IS NULL
        SET @CutOrderStatus = N'REGISTERED';

    IF @ProcessingStage IS NULL
        SET @ProcessingStage = N'IN_YARD';

    IF @SyncStatus IS NULL
        SET @SyncStatus = N'SYNC_QUEUED';

    IF @IdempotencyKey IS NULL
        SET @IdempotencyKey = NEWID();

    IF @Notes IS NULL
    BEGIN
        SELECT TOP (1)
            @CarryForwardNotes = NULLIF(LTRIM(RTRIM(co.Notes)), N'')
        FROM dbo.cut_orders co
        WHERE co.ErpCutOrderId = @ErpCutOrderId
          AND ISNULL(co.IsDeleted, 0) = 1
          AND co.Notes IS NOT NULL
        ORDER BY COALESCE(co.UpdatedAt, co.CreatedAt) DESC, co.CreatedAt DESC;

        SET @Notes = @CarryForwardNotes;
    END;

    SELECT TOP (1)
        @CutOrderId = Id,
        @ExistingProcessingStage = ProcessingStage,
        @ExistingStatus = CutOrderStatus,
        @ExistingSessionId = WeighingSessionId,
        @ExistingIsCancelled = IsCancelled
    FROM dbo.cut_orders
    WHERE ErpCutOrderId = @ErpCutOrderId
      AND ISNULL(IsDeleted, 0) = 0
    ORDER BY CreatedAt DESC;

    IF @CutOrderId IS NOT NULL
    BEGIN
        IF @ExistingSessionId IS NOT NULL
            THROW 51011, N'Cut order dang gan voi luot can, khong duoc ERP update truc tiep. Hay dung luong soft delete / CO lai.', 1;

        IF ISNULL(@ExistingProcessingStage, N'') <> N'IN_YARD'
            THROW 51012, N'Cut order khong con o trang thai IN_YARD, khong duoc ERP update truc tiep.', 1;

        IF ISNULL(@ExistingStatus, N'') <> N'REGISTERED'
            THROW 51013, N'Cut order khong con o trang thai REGISTERED, khong duoc ERP update truc tiep.', 1;

        IF ISNULL(@ExistingIsCancelled, 0) = 1
            THROW 51014, N'Cut order da bi huy, khong duoc ERP update truc tiep.', 1;

        UPDATE dbo.cut_orders
        SET
            ErpRegistrationCode = @ErpRegistrationCode,
            CutOrderSource = @CutOrderSource,
            CutOrderStatus = @CutOrderStatus,
            TransactionType = @TransactionType,
            TransportMethod = @TransportMethod,
            VehiclePlate = @VehiclePlate,
            MoocNumber = @MoocNumber,
            ReceiverName = @ReceiverName,
            ReceiverIdNo = @ReceiverIdNo,
            CustomerCode = @CustomerCode,
            CustomerName = @CustomerName,
            Market = @Market,
            ProductCode = @ProductCode,
            ProductName = @ProductName,
            ProductType = @ProductType,
            OrderCode = @OrderCode,
            LotNo = @LotNo,
            RepresentativeName = @RepresentativeName,
            ConsumptionPlace = @ConsumptionPlace,
            LoadingPlace = @LoadingPlace,
            SealNo = @SealNo,
            PlannedWeight = @PlannedWeight,
            BagCount = CASE WHEN @BagCount IS NULL THEN NULL ELSE CAST(ROUND(@BagCount, 0) AS INT) END,
            Notes = @Notes,
            HasOverweightCase = ISNULL(@HasOverweightCase, HasOverweightCase),
            ProcessingStage = @ProcessingStage,
            SyncStatus = @SyncStatus,
            IdempotencyKey = COALESCE(@IdempotencyKey, IdempotencyKey),
            IsInboundProcessed = ISNULL(@IsInboundProcessed, 0),
            InboundProcessedAt = NULL,
            InboundErrorCode = NULL,
            InboundErrorMessage = NULL,
            LastSyncAttemptAt = NULL,
            LastSyncError = NULL,
            UpdatedAt = @NowUtc,
            UpdatedBy = @SystemUser
        WHERE Id = @CutOrderId;

        RETURN;
    END

    INSERT INTO dbo.cut_orders
    (
        Id,
        ErpCutOrderId,
        ErpRegistrationCode,
        CutOrderSource,
        CutOrderStatus,
        TransactionType,
        TransportMethod,
        VehiclePlate,
        MoocNumber,
        ReceiverName,
        ReceiverIdNo,
        CustomerCode,
        CustomerName,
        Market,
        ProductCode,
        ProductName,
        ProductType,
        OrderCode,
        LotNo,
        RepresentativeName,
        ConsumptionPlace,
        LoadingPlace,
        SealNo,
        PlannedWeight,
        BagCount,
        Notes,
        IsCancelled,
        IsDeleted,
        HasOverweightCase,
        ProcessingStage,
        SyncStatus,
        IdempotencyKey,
        AppVersion,
        IsInboundProcessed,
        InboundProcessedAt,
        InboundErrorCode,
        InboundErrorMessage,
        LastSyncAttemptAt,
        LastSyncError,
        CreatedAt,
        CreatedBy,
        UpdatedAt,
        UpdatedBy
    )
    VALUES
    (
        NEWID(),
        @ErpCutOrderId,
        @ErpRegistrationCode,
        @CutOrderSource,
        @CutOrderStatus,
        @TransactionType,
        @TransportMethod,
        @VehiclePlate,
        @MoocNumber,
        @ReceiverName,
        @ReceiverIdNo,
        @CustomerCode,
        @CustomerName,
        @Market,
        @ProductCode,
        @ProductName,
        @ProductType,
        @OrderCode,
        @LotNo,
        @RepresentativeName,
        @ConsumptionPlace,
        @LoadingPlace,
        @SealNo,
        @PlannedWeight,
        CASE WHEN @BagCount IS NULL THEN NULL ELSE CAST(ROUND(@BagCount, 0) AS INT) END,
        @Notes,
        ISNULL(@IsCancelled, 0),
        0,
        ISNULL(@HasOverweightCase, 0),
        @ProcessingStage,
        @SyncStatus,
        @IdempotencyKey,
        N'ERP_SQL_PROC',
        ISNULL(@IsInboundProcessed, 0),
        NULL,
        NULL,
        NULL,
        NULL,
        NULL,
        @CreateUtc,
        COALESCE(NULLIF(LTRIM(RTRIM(@CreatedBy)), N''), @SystemUser),
        @NowUtc,
        @SystemUser
    );
END
GO
