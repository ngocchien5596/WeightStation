-- Script to ensure default settings for Crusher Weighing are properly configured
-- This fixes the issue where opening the CrusherWeighing screen crashes

-- 1. Ensure all stations have default weighing mode set to TWO_WEIGH
INSERT INTO station_operation_settings (station_code, setting_key, setting_value, created_at, created_by, updated_at, updated_by)
SELECT
    station_code,
    'crusher_default_weigh_mode',
    'TWO_WEIGH',
    GETDATE(),
    'SYSTEM',
    GETDATE(),
    'SYSTEM'
FROM stations
WHERE station_code NOT IN (
    SELECT station_code
    FROM station_operation_settings
    WHERE setting_key = 'crusher_default_weigh_mode'
);

-- 2. Ensure default product code is set for crusher (optional, adjust as needed)
-- Uncomment and modify if you have a default product for crusher weighing
/*
INSERT INTO station_operation_settings (station_code, setting_key, setting_value, created_at, created_by, updated_at, updated_by)
SELECT
    station_code,
    'crusher_default_product_code',
    'DA_01', -- Adjust this to your actual default product code
    GETDATE(),
    'SYSTEM',
    GETDATE(),
    'SYSTEM'
FROM stations
WHERE station_code NOT IN (
    SELECT station_code
    FROM station_operation_settings
    WHERE setting_key = 'crusher_default_product_code'
);
*/

-- 3. Verify the settings
SELECT
    s.station_code,
    s.station_name,
    s1.setting_value AS crusher_default_weigh_mode,
    s2.setting_value AS crusher_single_weigh_enabled,
    s3.setting_value AS crusher_require_standard_tare,
    s4.setting_value AS crusher_standard_tare_tolerance_kg
FROM stations s
LEFT JOIN station_operation_settings s1 ON s.station_code = s1.station_code AND s1.setting_key = 'crusher_default_weigh_mode'
LEFT JOIN station_operation_settings s2 ON s.station_code = s2.station_code AND s2.setting_key = 'crusher_single_weigh_enabled'
LEFT JOIN station_operation_settings s3 ON s.station_code = s3.station_code AND s3.setting_key = 'crusher_require_standard_tare_for_single_weigh'
LEFT JOIN station_operation_settings s4 ON s.station_code = s4.station_code AND s4.setting_key = 'crusher_standard_tare_tolerance_kg'
WHERE s.is_active = 1
ORDER BY s.station_code;