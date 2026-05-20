using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using Vyron.Shared.Enums;

namespace Vyron.Admin.Models;

// ─── STORE ────────────────────────────────────────────────────────
public class StoreCreateVm
{
    [Required(ErrorMessage = "Store name is required.")]
    [MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [Required(ErrorMessage = "Contact phone is required.")]
    [MaxLength(20)]
    public string Phone { get; set; } = string.Empty;

    [EmailAddress]
    [MaxLength(200)]
    public string? Email { get; set; }

    [Required(ErrorMessage = "Address is required.")]
    [MaxLength(300)]
    public string Address { get; set; } = string.Empty;

    [Required(ErrorMessage = "Area is required.")]
    [MaxLength(100)]
    public string Area { get; set; } = string.Empty;

    [MaxLength(100)]
    public string City { get; set; } = "Lagos";

    [MaxLength(100)]
    public string State { get; set; } = "Lagos";

    [Range(0, 9999999)]
    public decimal PickupFee { get; set; } = 1000;

    [Range(0, 9999999)]
    public decimal DeliveryFee { get; set; } = 1000;

    [MaxLength(100)]
    public string? OpeningHours { get; set; } = "8:00 AM - 8:00 PM";

    public Guid? OwnerId { get; set; }     // Admin sets this; StoreOwner leaves null (uses CurrentUserId)

    public IFormFile? ImageFile { get; set; }
}

public class StoreEditVm
{
    public Guid Id { get; set; }

    [Required(ErrorMessage = "Store name is required.")]
    [MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [Required(ErrorMessage = "Contact phone is required.")]
    [MaxLength(20)]
    public string Phone { get; set; } = string.Empty;

    [EmailAddress]
    [MaxLength(200)]
    public string? Email { get; set; }

    [Required(ErrorMessage = "Address is required.")]
    [MaxLength(300)]
    public string Address { get; set; } = string.Empty;

    [Required(ErrorMessage = "Area is required.")]
    [MaxLength(100)]
    public string Area { get; set; } = string.Empty;

    [MaxLength(100)]
    public string City { get; set; } = "Lagos";

    [MaxLength(100)]
    public string State { get; set; } = "Lagos";

    [Range(0, 9999999)]
    public decimal PickupFee { get; set; }

    [Range(0, 9999999)]
    public decimal DeliveryFee { get; set; }

    [MaxLength(100)]
    public string? OpeningHours { get; set; }

    public IFormFile? ImageFile { get; set; }
    public string? CurrentLogoUrl { get; set; }
}

// ─── SERVICE OFFERING ─────────────────────────────────────────────
public class ServiceOfferingCreateVm
{
    public Guid StoreId { get; set; }

    [Required(ErrorMessage = "Service name is required.")]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    [MaxLength(100)]
    public string? Category { get; set; }

    public ServiceType ServiceType { get; set; }

    // New: FK to admin-managed ServiceTypeEntity (nullable for backwards compat)
    public Guid? ServiceTypeEntityId { get; set; }

    public PricingMode PricingMode { get; set; } = PricingMode.PerKg;

    [Range(0, 9999999, ErrorMessage = "Price must be zero or greater.")]
    public decimal BasePrice { get; set; }

    [Range(0, 9999999)]
    public decimal MinimumCharge { get; set; }

    [Range(1, 9999)]
    public int EstimatedHours { get; set; } = 24;

    public bool IsActive { get; set; } = true;
}

public class ServiceOfferingEditVm : ServiceOfferingCreateVm
{
    public Guid Id { get; set; }
}

// ─── PROFILE ──────────────────────────────────────────────────────
public class ProfileEditVm
{
    [Required(ErrorMessage = "Full name is required.")]
    [MaxLength(120)]
    public string FullName { get; set; } = string.Empty;

    [MaxLength(10)]
    public string CountryCode { get; set; } = "+234";

    [MaxLength(20)]
    public string? LocalPhone { get; set; }

    [EmailAddress]
    [MaxLength(200)]
    public string? Email { get; set; }

    public IFormFile? ProfileImageFile { get; set; }
    public string? CurrentProfilePhoto { get; set; }
}

// ─── CHANGE PASSWORD ──────────────────────────────────────────────
public class ChangePasswordVm
{
    [Required(ErrorMessage = "Current password is required.")]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "New password is required.")]
    [MinLength(8, ErrorMessage = "Password must be at least 8 characters.")]
    public string NewPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please confirm your new password.")]
    [Compare(nameof(NewPassword), ErrorMessage = "Passwords do not match.")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

// ─── FORGOT / RESET PASSWORD ──────────────────────────────────────
public class ForgotPasswordVm
{
    [Required(ErrorMessage = "Email or phone is required.")]
    [MaxLength(200)]
    public string Identifier { get; set; } = string.Empty;
}

public class ResetPasswordVm
{
    [Required]
    public string Token { get; set; } = string.Empty;

    [Required(ErrorMessage = "New password is required.")]
    [MinLength(8, ErrorMessage = "Password must be at least 8 characters.")]
    public string NewPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please confirm your new password.")]
    [Compare(nameof(NewPassword), ErrorMessage = "Passwords do not match.")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

// ─── RIDER ────────────────────────────────────────────────────────
public class RiderCreateVm
{
    [Required(ErrorMessage = "Full name is required.")]
    [MaxLength(120)]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Country code is required.")]
    public string CountryCode { get; set; } = "+234";

    [Required(ErrorMessage = "Phone number is required.")]
    [MaxLength(20)]
    public string LocalPhone { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vehicle type is required.")]
    [MaxLength(50)]
    public string VehicleType { get; set; } = "Motorcycle";

    [MaxLength(20)]
    public string? VehiclePlate { get; set; }

    public IFormFile? ProfileImageFile { get; set; }
}

public class RiderEditVm
{
    public Guid Id { get; set; }

    [Required(ErrorMessage = "Full name is required.")]
    [MaxLength(120)]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vehicle type is required.")]
    [MaxLength(50)]
    public string VehicleType { get; set; } = "Motorcycle";

    [MaxLength(20)]
    public string? VehiclePlate { get; set; }

    public RiderStatus Status { get; set; }

    public IFormFile? ProfileImageFile { get; set; }
    public string? CurrentProfilePhoto { get; set; }
}

// ─── USERS MODULE ─────────────────────────────────────────────────
public class UserListItemVm
{
    public Guid    Id           { get; set; }
    public string  FullName     { get; set; } = string.Empty;
    public string  Phone        { get; set; } = string.Empty;
    public string? Email        { get; set; }
    public string  Roles        { get; set; } = string.Empty;  // comma-joined role names
    public bool    IsActive     { get; set; }
    public DateTime CreatedAt   { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public string? ProfilePhoto { get; set; }
}

public class UserDetailVm : UserListItemVm
{
    public List<string> RoleList  { get; set; } = new();
    public int    TotalOrders     { get; set; }
    public int    TotalReviews    { get; set; }
    public int    TotalDisputes   { get; set; }
    public string CountryCode     { get; set; } = "+234";
    public string? LocalPhone     { get; set; }
}

public class UsersFilterVm
{
    public string? Search    { get; set; }
    public string? RoleFilter { get; set; }  // "Customer","Admin","StoreOwner","Rider",""
    public int     Page       { get; set; } = 1;
    public int     PageSize   { get; set; } = 20;
    public int     TotalCount { get; set; }
    public List<UserListItemVm> Users { get; set; } = new();
}

// ─── ADMIN USER MANAGEMENT ────────────────────────────────────────
public class AdminUserCreateVm
{
    [Required, MaxLength(120)]
    public string FullName { get; set; } = string.Empty;

    [Required, MaxLength(20)]
    public string Phone { get; set; } = string.Empty;

    [EmailAddress, MaxLength(200)]
    public string? Email { get; set; }

    [Required, MinLength(8)]
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// Role to assign: "AdminUser", "Admin", or "SuperAdmin".
    /// SuperAdmin is only honoured when the creating user is already SuperAdmin.
    /// </summary>
    [Required]
    public string SelectedRole { get; set; } = "AdminUser";

    // Legacy helper kept for backward compatibility (not used in new Create flow)
    public bool IsAdminUser => SelectedRole == "AdminUser";
}

// ─── STORE STAFF ──────────────────────────────────────────────────
public class StoreStaffCreateVm
{
    [Required, MaxLength(120)]
    public string FullName { get; set; } = string.Empty;

    [Required, MaxLength(20)]
    public string Phone { get; set; } = string.Empty;

    [Required, MinLength(6)]
    public string Password { get; set; } = string.Empty;

    [Required]
    public Guid StoreId { get; set; }

    /// <summary>True = StoreManager, False = StoreStaff.</summary>
    public bool IsManager { get; set; } = false;
}
