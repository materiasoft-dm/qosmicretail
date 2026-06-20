IF NOT EXISTS(SELECT * FROM sys.columns WHERE Name = N'WholeSaleId' and Object_ID = Object_ID(N'InvoiceItems'))
BEGIN
    ALTER TABLE InvoiceItems ADD WholeSaleId int NULL
END