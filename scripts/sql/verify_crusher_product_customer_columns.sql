-- Verification script for Crusher Weighing Product and Customer feature
-- This script checks if the new columns have been added correctly to weighing_sessions table

-- 1. Check for new columns in weighing_sessions table
SELECT
    COLUMN_NAME,
    DATA_TYPE,
    CHARACTER_MAXIMUM_LENGTH,
    IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'weighing_sessions'
    AND COLUMN_NAME IN ('ProductCode', 'ProductName', 'CustomerCode', 'CustomerName')
ORDER BY COLUMN_NAME;

-- 2. Check existing weighing_sessions to see if columns are populated
SELECT TOP 10
    SessionNo,
    VehiclePlate,
    ProductCode,
    ProductName,
    CustomerCode,
    CustomerName,
    SessionStatus,
    CreatedAt
FROM weighing_sessions
WHERE InternalVehicleNo IS NOT NULL
ORDER BY CreatedAt DESC;

-- 3. Check if default crusher weighing settings exist
SELECT
    station_code,
    setting_key,
    setting_value,
    created_at,
    updated_at
FROM station_operation_settings
WHERE setting_key IN (
    'crusher_default_weigh_mode',
    'crusher_default_product_code',
    'crusher_single_weigh_enabled'
)
ORDER BY station_code, setting_key;