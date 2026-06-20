IF NOT EXISTS(SELECT * FROM sys.columns WHERE Name = N'Filenames' and Object_ID = Object_ID(N'BulkPackages'))
BEGIN
	ALTER TABLE BulkPackages ADD Filenames varchar(max) NULL
END