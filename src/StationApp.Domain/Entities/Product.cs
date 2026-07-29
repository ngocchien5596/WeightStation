using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace StationApp.Domain.Entities;

public class Product
{
    public Guid Id { get; set; }
    public string StationCode { get; set; } = string.Empty;
    public string ProductCode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string? ProductType { get; set; }
    public string TransactionScope { get; set; } = Constants.ProductTransactionScopes.Both;
    [NotMapped]
    [JsonIgnore]
    public string TransactionScopeDisplay => Constants.ProductTransactionScopes.ToDisplay(TransactionScope);
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}
