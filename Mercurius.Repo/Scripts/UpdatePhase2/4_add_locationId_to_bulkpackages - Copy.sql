IF NOT EXISTS(SELECT * FROM sys.columns WHERE Name = N'LocationId' and Object_ID = Object_ID(N'BulkPackages'))
BEGIN
	ALTER TABLE BulkPackages ADD LocationId int NOT NULL
END