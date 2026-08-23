SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @Today date = '2026-08-23';
DECLARE @SeedPrefix nvarchar(32) = N'SEED-CS-';
DECLARE @PrimaryCustomerId uniqueidentifier =
    (SELECT TOP (1) Id FROM dbo.Customers ORDER BY Name);

BEGIN TRANSACTION;

DELETE sli
FROM dbo.SaleLineItems sli
INNER JOIN dbo.SalesBills sb ON sb.Id = sli.SalesBillId
WHERE sb.BillNo LIKE @SeedPrefix + N'%';

DELETE FROM dbo.SalesBills
WHERE BillNo LIKE @SeedPrefix + N'%';

IF OBJECT_ID('tempdb..#SeedMaterials') IS NOT NULL
    DROP TABLE #SeedMaterials;

CREATE TABLE #SeedMaterials
(
    RowNo int IDENTITY(1,1) PRIMARY KEY,
    MaterialId nvarchar(20) NOT NULL,
    Barcode nvarchar(100) NOT NULL,
    MaterialName nvarchar(200) NOT NULL,
    Category nvarchar(100) NOT NULL,
    Packing nvarchar(100) NOT NULL,
    SaleRate decimal(18,2) NOT NULL,
    TaxPercent decimal(18,2) NOT NULL
);

INSERT INTO #SeedMaterials (MaterialId, Barcode, MaterialName, Category, Packing, SaleRate, TaxPercent)
SELECT TOP (18)
    m.Id,
    m.Barcode,
    m.Name,
    m.Category,
    m.Packing,
    m.SaleRate,
    m.TaxPercent
FROM dbo.Materials m
WHERE ISNULL(m.Name, N'') <> N''
  AND ISNULL(m.Barcode, N'') <> N''
  AND ISNULL(m.Category, N'') <> N''
ORDER BY
    CASE
        WHEN m.Category LIKE N'%beer%' THEN 0
        WHEN m.Category LIKE N'%whisk%' THEN 1
        WHEN m.Category LIKE N'%rum%' THEN 2
        WHEN m.Category LIKE N'%vodka%' THEN 3
        ELSE 4
    END,
    m.Name;

IF NOT EXISTS (SELECT 1 FROM #SeedMaterials)
BEGIN
    ROLLBACK TRANSACTION;
    THROW 50001, 'No materials found in dbo.Materials. Seed materials first, then run this script again.', 1;
END;

IF OBJECT_ID('tempdb..#BillPlan') IS NOT NULL
    DROP TABLE #BillPlan;

CREATE TABLE #BillPlan
(
    DayDate date NOT NULL PRIMARY KEY,
    BillsToCreate int NOT NULL,
    Intensity decimal(10,2) NOT NULL
);

;WITH recent_days AS
(
    SELECT TOP (30)
        ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) - 1 AS n
    FROM sys.all_objects
)
INSERT INTO #BillPlan (DayDate, BillsToCreate, Intensity)
SELECT
    DATEADD(day, -(29 - n), @Today) AS DayDate,
    CASE
        WHEN n = 29 THEN 8
        WHEN n >= 26 THEN 6
        WHEN n >= 20 THEN 4
        WHEN n >= 12 THEN 3
        ELSE 2
    END AS BillsToCreate,
    CASE
        WHEN n = 29 THEN 1.35
        WHEN n >= 26 THEN 1.18
        WHEN n >= 20 THEN 1.05
        ELSE 0.92
    END AS Intensity
FROM recent_days;

;WITH older_pulses AS
(
    SELECT TOP (18)
        ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) - 1 AS n
    FROM sys.all_objects
)
INSERT INTO #BillPlan (DayDate, BillsToCreate, Intensity)
SELECT
    DATEADD(day, -(45 + (n * 17)), @Today) AS DayDate,
    1 + (n % 2) AS BillsToCreate,
    0.88 + ((n % 3) * 0.07) AS Intensity
FROM older_pulses
WHERE NOT EXISTS
(
    SELECT 1
    FROM #BillPlan bp
    WHERE bp.DayDate = DATEADD(day, -(45 + (n * 17)), @Today)
);

DECLARE
    @DayDate date,
    @BillsToCreate int,
    @Intensity decimal(10,2),
    @BillIndex int,
    @BillId uniqueidentifier,
    @BillNo nvarchar(50),
    @CreatedAt datetime2(0),
    @PayMode nvarchar(30),
    @CustomerId uniqueidentifier,
    @LineCount int,
    @TaxableValue decimal(18,2),
    @TotalDiscount decimal(18,2),
    @TotalTax decimal(18,2),
    @TotalAmount decimal(18,2);

DECLARE bill_cursor CURSOR LOCAL FAST_FORWARD FOR
SELECT DayDate, BillsToCreate, Intensity
FROM #BillPlan
ORDER BY DayDate;

OPEN bill_cursor;
FETCH NEXT FROM bill_cursor INTO @DayDate, @BillsToCreate, @Intensity;

WHILE @@FETCH_STATUS = 0
BEGIN
    SET @BillIndex = 1;

    WHILE @BillIndex <= @BillsToCreate
    BEGIN
        SET @BillId = NEWID();
        SET @BillNo =
            @SeedPrefix
            + REPLACE(CONVERT(varchar(10), @DayDate, 112), '-', '')
            + N'-'
            + RIGHT(N'00' + CAST(@BillIndex AS nvarchar(10)), 2);
        SET @CreatedAt = DATEADD(
            minute,
            (35 * @BillIndex) + (ABS(CHECKSUM(CONCAT(CONVERT(varchar(10), @DayDate, 120), '-', @BillIndex))) % 20),
            CAST(@DayDate AS datetime2(0))
        );
        SET @PayMode =
            CASE
                WHEN @BillIndex % 5 = 0 THEN N'UPI'
                WHEN @BillIndex % 3 = 0 THEN N'Card'
                ELSE N'Cash'
            END;
        SET @CustomerId =
            CASE
                WHEN @PrimaryCustomerId IS NOT NULL AND @BillIndex % 4 = 0 THEN @PrimaryCustomerId
                ELSE NULL
            END;
        SET @LineCount =
            CASE
                WHEN @BillIndex % 3 = 0 THEN 3
                ELSE 2
            END;

        DECLARE @BillLines TABLE
        (
            LineNumber int NOT NULL,
            MaterialId nvarchar(20) NOT NULL,
            BarcodeNo nvarchar(100) NOT NULL,
            MaterialType nvarchar(100) NOT NULL,
            MaterialName nvarchar(200) NOT NULL,
            Packing nvarchar(100) NOT NULL,
            Quantity int NOT NULL,
            Rate decimal(18,2) NOT NULL,
            DiscountAmount decimal(18,2) NOT NULL,
            TaxPercent decimal(18,2) NOT NULL,
            TaxAmount decimal(18,2) NOT NULL,
            Amount decimal(18,2) NOT NULL
        );

        INSERT INTO @BillLines
        (
            LineNumber,
            MaterialId,
            BarcodeNo,
            MaterialType,
            MaterialName,
            Packing,
            Quantity,
            Rate,
            DiscountAmount,
            TaxPercent,
            TaxAmount,
            Amount
        )
        SELECT
            ROW_NUMBER() OVER (ORDER BY PickRank) AS LineNumber,
            MaterialId,
            Barcode,
            Category,
            MaterialName,
            Packing,
            Qty,
            Rate,
            DiscountAmount,
            TaxPercent,
            ROUND(((Qty * Rate) - DiscountAmount) * TaxPercent / 100.0, 2) AS TaxAmount,
            ROUND(((Qty * Rate) - DiscountAmount) + (((Qty * Rate) - DiscountAmount) * TaxPercent / 100.0), 2) AS Amount
        FROM
        (
            SELECT TOP (@LineCount)
                sm.MaterialId,
                sm.Barcode,
                sm.Category,
                sm.MaterialName,
                sm.Packing,
                CAST(ROUND(sm.SaleRate * @Intensity, 2) AS decimal(18,2)) AS Rate,
                CASE
                    WHEN sm.Packing LIKE N'%30%' OR sm.Packing LIKE N'%60%' OR sm.Packing LIKE N'%90%' THEN 2 + (ABS(CHECKSUM(CONCAT(sm.MaterialId, @BillIndex, @DayDate))) % 6)
                    WHEN sm.Packing LIKE N'%180%' THEN 1 + (ABS(CHECKSUM(CONCAT(sm.MaterialId, @BillIndex, @DayDate, '180'))) % 4)
                    WHEN sm.Packing LIKE N'%330%' OR sm.Packing LIKE N'%375%' THEN 2 + (ABS(CHECKSUM(CONCAT(sm.MaterialId, @BillIndex, @DayDate, 'small'))) % 5)
                    WHEN sm.Packing LIKE N'%500%' OR sm.Packing LIKE N'%650%' THEN 1 + (ABS(CHECKSUM(CONCAT(sm.MaterialId, @BillIndex, @DayDate, 'beer'))) % 4)
                    ELSE 1 + (ABS(CHECKSUM(CONCAT(sm.MaterialId, @BillIndex, @DayDate, 'default'))) % 3)
                END AS Qty,
                CASE
                    WHEN (@BillIndex + sm.RowNo) % 6 = 0 THEN CAST(ROUND(sm.SaleRate * 0.03, 2) AS decimal(18,2))
                    ELSE CAST(0 AS decimal(18,2))
                END AS DiscountAmount,
                sm.TaxPercent,
                ABS(CHECKSUM(CONCAT(CONVERT(varchar(10), @DayDate, 120), '-', @BillIndex, '-', sm.MaterialId))) AS PickRank
            FROM #SeedMaterials sm
            ORDER BY PickRank
        ) picked;

        SELECT
            @TaxableValue = ROUND(SUM((Quantity * Rate) - DiscountAmount), 2),
            @TotalDiscount = ROUND(SUM(DiscountAmount), 2),
            @TotalTax = ROUND(SUM(TaxAmount), 2),
            @TotalAmount = ROUND(SUM(Amount), 2)
        FROM @BillLines;

        INSERT INTO dbo.SalesBills
        (
            Id,
            BillNo,
            CustomerId,
            BillType,
            BillDate,
            PayMode,
            TaxableValue,
            TotalDiscount,
            TotalTax,
            TotalAmount,
            BalanceDue,
            Status,
            CreatedAt,
            UpdatedAt,
            IsSynced,
            IsDeleted
        )
        VALUES
        (
            @BillId,
            @BillNo,
            @CustomerId,
            N'CounterSale.Sale',
            @DayDate,
            @PayMode,
            @TaxableValue,
            @TotalDiscount,
            @TotalTax,
            @TotalAmount,
            0,
            N'completed',
            @CreatedAt,
            DATEADD(minute, 3, @CreatedAt),
            1,
            0
        );

        INSERT INTO dbo.SaleLineItems
        (
            Id,
            SalesBillId,
            BarcodeNo,
            MaterialType,
            MaterialName,
            BatchNo,
            Packing,
            Quantity,
            Rate,
            DiscountPercent,
            DiscountAmount,
            TaxPercent,
            TaxAmount,
            Amount,
            LineNumber,
            CreatedAt,
            UpdatedAt,
            IsDeleted,
            MaterialId,
            QtyCase
        )
        SELECT
            NEWID(),
            @BillId,
            BarcodeNo,
            MaterialType,
            MaterialName,
            NULL,
            Packing,
            Quantity,
            Rate,
            NULL,
            DiscountAmount,
            TaxPercent,
            TaxAmount,
            Amount,
            LineNumber,
            @CreatedAt,
            DATEADD(minute, 2, @CreatedAt),
            0,
            MaterialId,
            0
        FROM @BillLines;

        SET @BillIndex += 1;
    END;

    FETCH NEXT FROM bill_cursor INTO @DayDate, @BillsToCreate, @Intensity;
END;

CLOSE bill_cursor;
DEALLOCATE bill_cursor;

COMMIT TRANSACTION;

SELECT
    COUNT(*) AS SeededBills,
    MIN(BillDate) AS FirstBillDate,
    MAX(BillDate) AS LastBillDate,
    SUM(TotalAmount) AS TotalSeededAmount
FROM dbo.SalesBills
WHERE BillNo LIKE @SeedPrefix + N'%';

SELECT TOP (15)
    BillDate,
    COUNT(*) AS Bills,
    SUM(TotalAmount) AS Amount
FROM dbo.SalesBills
WHERE BillNo LIKE @SeedPrefix + N'%'
GROUP BY BillDate
ORDER BY BillDate DESC;
