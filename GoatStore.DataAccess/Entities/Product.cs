using System;

namespace GoatStore.DataAccess.Entities
{
    public enum ProductStatus
    {
        InStock,
        Sold
    }

    public enum ProductCondition
    {
        New,
        Used
    }

    public class Product
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Brand { get; set; } = string.Empty;
        public string Size { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public ProductCondition Condition { get; set; } = ProductCondition.New;
        public string? Sku { get; set; }
        public decimal PurchasePrice { get; set; }
        public decimal SalePrice { get; set; }
        public ProductStatus Status { get; set; } = ProductStatus.InStock;
        public DateTime CreatedAt { get; set; }
        public DateTime? SoldDate { get; set; }
        public Guid StoreId { get; set; }
        public Store? Store { get; set; }
    }
}
