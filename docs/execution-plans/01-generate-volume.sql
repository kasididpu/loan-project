-- Synthetic volume for execution-plan evidence (dev database only).
-- 100,000 payments spread across 1,000 synthetic loans. Every id is
-- derived deterministically from the series value, so a rerun collides
-- on the primary key and fails fast instead of doubling the data.
-- GENERATE_SERIES requires SQL Server 2022+ / Azure SQL (compat 160).
INSERT INTO Payment (Id, LoanId, Amount, StripeEventId, PaidAtUtc)
SELECT
    CONVERT(uniqueidentifier, HASHBYTES('MD5', CONCAT('payment-', s.value))),
    CONVERT(uniqueidentifier, HASHBYTES('MD5', CONCAT('loan-', s.value % 1000))),
    100.00 + CAST(s.value % 9000 AS decimal(9, 2)) / 100,          -- 100.00-189.99, satang-exact
    CONCAT('evt_vol_', s.value),                                    -- unique by construction
    DATEADD(minute, s.value % 525600, '2025-01-01')                 -- 100k minutes ~= 69 days (Jan-Mar 2025)
FROM GENERATE_SERIES(1, 100000) AS s;
GO
