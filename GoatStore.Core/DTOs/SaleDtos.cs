using System.Text.Json.Serialization;
using GoatStore.DataAccess.Entities;

namespace GoatStore.Core.DTOs
{
    public class CreateSaleItemDto
    {
        public Guid ProductId { get; set; }
        public int Quantity { get; set; } = 1;
    }

    public class CreateSaleRequestDto
    {
        public List<CreateSaleItemDto> Items { get; set; } = [];
        public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Card;
        public decimal Discount { get; set; }
    }

    public class SaleItemDto
    {
        public Guid Id { get; set; }
        public Guid ProductId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Sku { get; set; }
        public int Quantity { get; set; }
        public int ReturnedQuantity { get; set; }
        public decimal UnitPrice { get; set; }
    }

    public class SaleDto
    {
        public Guid Id { get; set; }
        public int Number { get; set; }
        public string Channel { get; set; } = string.Empty;
        public PaymentMethod PaymentMethod { get; set; }
        public decimal Subtotal { get; set; }
        public decimal Discount { get; set; }
        public decimal Total { get; set; }
        public decimal RefundedTotal { get; set; }
        public DateTime CreatedAt { get; set; }
        public IReadOnlyList<SaleItemDto> Items { get; set; } = [];
    }

    public class ReturnItemDto
    {
        public Guid SaleItemId { get; set; }
        public int Quantity { get; set; } = 1;
    }

    public class ReturnSaleRequestDto
    {
        public List<ReturnItemDto> Items { get; set; } = [];
    }

    public enum SaleError
    {
        None = 0,
        Validation,
        NotFound,
        Conflict
    }

    public class SaleResponseDto
    {
        public bool IsSuccess { get; set; }
        public string Message { get; set; } = string.Empty;
        public SaleDto? Sale { get; set; }

        [JsonIgnore]
        public SaleError Error { get; set; }
    }
}
