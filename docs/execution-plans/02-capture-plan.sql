-- Captures the actual execution plan as text (STATISTICS PROFILE) for the
-- two real query patterns of the Payment table. Run via sqlcmd and
-- redirect the output to a before/after evidence file.
SET STATISTICS PROFILE ON;
GO

-- Query A: the statement view (PaymentRepository.ListByLoanAsync).
-- Before IX_Payment_LoanId_PaidAtUtc: Table Scan + explicit Sort.
-- After: Index Seek, and the Sort operator disappears (rows arrive ordered).
DECLARE @LoanId uniqueidentifier =
    CONVERT(uniqueidentifier, HASHBYTES('MD5', 'loan-42'));
SELECT Id, LoanId, Amount, StripeEventId, PaidAtUtc
FROM Payment
WHERE LoanId = @LoanId
ORDER BY PaidAtUtc;
GO

-- Query B: end-of-day aggregation (heart of usp_GetEndOfDayLoanSummary).
-- The date range is written as a half-open interval on the raw column —
-- wrapping PaidAtUtc in CAST/CONVERT would make it non-sargable and force
-- a scan no matter what indexes exist.
SELECT LoanId, COUNT(*) AS PaymentsToday, SUM(Amount) AS CollectedToday
FROM Payment
WHERE PaidAtUtc >= '2025-02-01' AND PaidAtUtc < '2025-02-02'
GROUP BY LoanId;
GO

SET STATISTICS PROFILE OFF;
GO
