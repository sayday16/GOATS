namespace GoatStore.DataAccess.Entities
{
    public enum PaymentMethod
    {
        Card,
        Cash
    }

    public class Sale
    {
        public Guid Id { get; set; }
        public int Number { get; set; }
        public string Channel { get; set; } = "Каса";
        public PaymentMethod PaymentMethod { get; set; }
        public decimal Discount { get; set; }
        public DateTime CreatedAt { get; set; }
        public Guid StoreId { get; set; }
        public Store? Store { get; set; }
        public Guid SoldByUserId { get; set; }
        public User? SoldBy { get; set; }
        public List<SaleItem> Items { get; set; } = new();
    }

    public class SaleItem
    {
        public Guid Id { get; set; }
        public Guid SaleId { get; set; }
        public Sale? Sale { get; set; }
        public Guid ProductId { get; set; }
        public Product? Product { get; set; }
        public int Quantity { get; set; }
        public int ReturnedQuantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal PurchasePrice { get; set; }
    }
}
