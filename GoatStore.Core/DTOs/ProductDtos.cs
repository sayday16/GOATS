using System.Text.Json.Serialization;
using GoatStore.DataAccess.Entities;

namespace GoatStore.Core.DTOs
{
    public class SaveProductRequestDto
    {
        public string Title { get; set; } = string.Empty;
        public decimal PurchasePrice { get; set; }
        public decimal SalePrice { get; set; }
    }

    public class ProductDto
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public decimal PurchasePrice { get; set; }
        public decimal SalePrice { get; set; }
        public ProductStatus Status { get; set; }
        public DateTime? SoldDate { get; set; }
    }

    public enum ProductError
    {
        None = 0,
        Validation,  // 400
        NotFound,    // 404
        Conflict     // 409
    }

    public class ProductResponseDto
    {
        public bool IsSuccess { get; set; }
        public string Message { get; set; } = string.Empty;
        public ProductDto? Product { get; set; }

        [JsonIgnore]
        public ProductError Error { get; set; }
    }
}
