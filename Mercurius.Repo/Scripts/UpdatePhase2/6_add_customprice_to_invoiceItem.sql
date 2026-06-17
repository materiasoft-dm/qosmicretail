IF NOT EXISTS(SELECT * FROM sys.columns WHERE Name = N'CustomTotalPrice' and Object_ID = Object_ID(N'InvoiceItems'))
BEGIN
	ALTER TABLE InvoiceItems ADD CustomTotalPrice decimal(8,2) NULL
END