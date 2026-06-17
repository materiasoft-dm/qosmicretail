/****** Object:  Table [dbo].[BulkPackages]    Script Date: 27/07/2023 3:11:38 pm ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[BulkPackages](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[ProductId] [int] NOT NULL,
	[QuantityInPackage] [int] NOT NULL,
	[SalePrice] [decimal](18, 2) NOT NULL,
	[Description] [varchar](max) NULL,
	[IsActive] [bit] NOT NULL,
	[DateCreated] [datetime] NOT NULL,
	[CreatedBy] [uniqueidentifier] NOT NULL,
	[DateUpdated] [datetime] NULL,
	[UpdatedBy] [uniqueidentifier] NULL,
 CONSTRAINT [PK_BulkPackage] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
ALTER TABLE [dbo].[BulkPackages]  WITH CHECK ADD  CONSTRAINT [FK_BulkPackages_Products] FOREIGN KEY([ProductId])
REFERENCES [dbo].[Products] ([Id])
GO
ALTER TABLE [dbo].[BulkPackages] CHECK CONSTRAINT [FK_BulkPackages_Products]
GO
