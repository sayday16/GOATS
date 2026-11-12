using System.Text.Json.Serialization;
using GoatStore.DataAccess.Entities;

namespace GoatStore.Core.DTOs
{
    public class SaveProductRequestDto
    {
        public string Title { get; set; } = string.Empty;
        public string Brand { get; set; } = string.Empty;
        public string Size { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public ProductCondition Condition { get; set; } = ProductCondition.New;
        public string? Sku { get; set; }
        public decimal PurchasePrice { get; set; }
        public decimal SalePrice { get; set; }
    }

    public class ProductFilterDto
    {
        public string? Search { get; set; }
        public string? Category { get; set; }
        public string? Brand { get; set; }
        public ProductStatus? Status { get; set; }
    }

    public class ProductDto
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Brand { get; set; } = string.Empty;
        public string Size { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public ProductCondition Condition { get; set; }
        public string? Sku { get; set; }
        public decimal PurchasePrice { get; set; }
        public decimal SalePrice { get; set; }
        public ProductStatus Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? SoldDate { get; set; }
    }

    public class ProductFilterOptionsDto
    {
        public IReadOnlyList<string> Categories { get; set; } = [];
        public IReadOnlyList<string> Brands { get; set; } = [];
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
