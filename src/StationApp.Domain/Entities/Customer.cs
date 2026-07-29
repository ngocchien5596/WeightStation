using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace StationApp.Domain.Entities;

public class Customer
{
    public Guid Id { get; set; }
    public string StationCode { get; set; } = string.Empty;
    public string CustomerCode { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerBusinessRole { get; set; } = Constants.CustomerBusinessRoles.Both;
    [NotMapped]
    [JsonIgnore]
    public string CustomerBusinessRoleDisplay => Constants.CustomerBusinessRoles.ToDisplay(CustomerBusinessRole);
    [NotMapped]
    [JsonIgnore]
    public bool IsSupplierCustomer => Constants.CustomerBusinessRoles.AllowsTransaction(CustomerBusinessRole, Enums.TransactionType.INBOUND);
    [NotMapped]
    [JsonIgnore]
    public bool IsDistributorCustomer => Constants.CustomerBusinessRoles.AllowsTransaction(CustomerBusinessRole, Enums.TransactionType.OUTBOUND);
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}
