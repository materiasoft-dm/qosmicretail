IF NOT EXISTS(SELECT * FROM sys.columns WHERE Name = N'IsActive' and Object_ID = Object_ID(N'InvoiceRefunds'))
BEGIN
    ALTER TABLE InvoiceRefunds ADD IsActive bit NOT NULL default 0
END