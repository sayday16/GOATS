using System;

namespace GoatStore.DataAccess.Entities
{
    public enum ProductStatus
    {
        InStock,
        Sold
    }

    public class Product
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public decimal PurchasePrice { get; set; }
        public decimal SalePrice { get; set; }
        public ProductStatus Status { get; set; } = ProductStatus.InStock;
        public DateTime? SoldDate { get; set; }
    }
}