SET IDENTITY_INSERT InvoiceStatus ON;

INSERT INTO InvoiceStatus (Id, [Name], CssClass) VALUES(6,'Deferred Payment','badge badge-light-info');
INSERT INTO InvoiceStatus (Id, [Name], CssClass) VALUES(7,'Partially Paid','badge badge-info');

SET IDENTITY_INSERT InvoiceStatus OFF;