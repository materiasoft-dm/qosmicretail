IF NOT EXISTS(SELECT * FROM sys.columns WHERE Name = N'PaidAmount' and Object_ID = Object_ID(N'Invoices'))
BEGIN
	ALTER TABLE Invoices ADD PaidAmount decimal(8,2) NOT NULL default 0
END
ELSE
BEGIN
SELECT 'PaidAmount exist already in invoices'
END