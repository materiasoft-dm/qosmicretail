IF NOT EXISTS(SELECT * FROM sys.columns WHERE Name = N'IsWholeSale' and Object_ID = Object_ID(N'InvoiceItems'))
BEGIN
    ALTER TABLE InvoiceItems ADD IsWholeSale bit NOT NULL default 0
END