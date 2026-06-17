using System;
using System.ComponentModel.DataAnnotations;

namespace Mercurius.Repo.Models;

public class ZeroStockSaleAuditLog
{
    [Key]
    public int Id { get; set; }

    public int ProductId { get; set; }

    public string ProductName { get; set; }

    public string ProductPartCode { get; set; }

    public decimal QuantitySold { get; set; }

    public decimal StockAtTimeOfSale { get; set; }

    public string InvoiceNumber { get; set; }

    public int InvoiceId { get; set; }

    public Guid SoldByUserId { get; set; }

    public string SoldByUserName { get; set; }

    public DateTime SaleDate { get; set; }

    public string Notes { get; set; }
}