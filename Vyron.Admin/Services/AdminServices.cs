using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Vyron.Admin.Data;
using Vyron.API.Models;
using Vyron.Shared.Enums;

namespace Vyron.Admin.Services;

// ═══════════════════════════════════════════════════════════════════
// View models (server-rendered)
// ═══════════════════════════════════════════════════════════════════
public class DashboardVm
{
    public int OrdersToday { get; set; }
    public int PendingPickups { get; set; }
    public int Processing { get; set; }
    public int OutForDelivery { get; set; }
    public int ActiveDisputes { get; set; }
    public decimal RevenueToday { get; set; }
    public decimal RevenueWeek { get; set; }
    public decimal RevenueMonth { get; set; }
    public int TotalStores { get; set; }
    public int OnlineRiders { get; set; }
    public int TotalCustomers { get; set; }
    public List<Order> RecentOrders { get; set; } = new();
    public List<Dispute> OpenDisputes { get; set; } = new();
}

public class StoreOwnerDashboardVm
{
    public List<LaundryStore> MyStores { get; set; } = new();
    public int TotalOrdersToday { get; set; }
    public int PendingConfirmation { get; set; }
    public int Processing { get; set; }
    public int Ready { get; set; }
    public decimal RevenueToday { get; set; }
    public List<Order> RecentOrders { get; set; } = new();
}

// ═══════════════════════════════════════════════════════════════════
// Admin analytics
// ═══════════════════════════════════════════════════════════════════
public interface IAdminAnalytics
{
    Task<DashboardVm> GetDashboardAsync();
    Task<StoreOwnerDashboardVm> GetOwnerDashboardAsync(Guid ownerId);
}

public class AdminAnalytics : IAdminAnalytics
{
    private readonly AdminDbContext _db;
    public AdminAnalytics(AdminDbContext db) => _db = db;

    public async Task<DashboardVm> GetDashboardAsync()
    {
        var today = DateTime.UtcNow.Date;
        var weekStart = today.AddDays(-(int)today.DayOfWeek);
        var monthStart = new DateTime(today.Year, today.Month, 1);

        var orders = await _db.Orders.AsNoTracking().ToListAsync();
        decimal Total(Order o) => o.ActualTotal > 0 ? o.ActualTotal : o.TotalEstimate;

        return new DashboardVm
        {
            OrdersToday = orders.Count(o => o.CreatedAt.Date == today),
            PendingPickups = orders.Count(o => o.Status == OrderStatus.RiderAssigned
                                          || o.Status == OrderStatus.PickUpRiderAssigned),
            Processing = orders.Count(o => o.Status == OrderStatus.Processing),
            OutForDelivery = orders.Count(o => o.Status == OrderStatus.OutForDelivery),
            ActiveDisputes = await _db.Disputes.CountAsync(d =>
                d.Status == DisputeStatus.Open || d.Status == DisputeStatus.UnderReview),
            RevenueToday = orders.Where(o => o.CreatedAt.Date == today && o.PaymentState == PaymentState.FullyPaid).Sum(Total),
            RevenueWeek = orders.Where(o => o.CreatedAt >= weekStart && o.PaymentState == PaymentState.FullyPaid).Sum(Total),
            RevenueMonth = orders.Where(o => o.CreatedAt >= monthStart && o.PaymentState == PaymentState.FullyPaid).Sum(Total),
            TotalStores = await _db.Stores.CountAsync(s => s.Status == StoreStatus.Active),
            OnlineRiders = await _db.Riders.CountAsync(r => r.Status == RiderStatus.OnlineAvailable),
            TotalCustomers = await _db.Users.CountAsync(u => u.Role == UserRole.Customer),
            RecentOrders = await _db.Orders.Include(o => o.Customer).Include(o => o.Store)
                .OrderByDescending(o => o.CreatedAt).Take(10).AsNoTracking().ToListAsync(),
            OpenDisputes = await _db.Disputes.Include(d => d.Order).ThenInclude(o => o.Customer)
                .Where(d => d.Status == DisputeStatus.Open || d.Status == DisputeStatus.UnderReview)
                .OrderByDescending(d => d.CreatedAt).Take(5).AsNoTracking().ToListAsync()
        };
    }

    public async Task<StoreOwnerDashboardVm> GetOwnerDashboardAsync(Guid ownerId)
    {
        var stores = await _db.Stores.Where(s => s.OwnerId == ownerId).AsNoTracking().ToListAsync();
        var storeIds = stores.Select(s => s.Id).ToList();
        var today = DateTime.UtcNow.Date;
        var orders = await _db.Orders
            .Include(o => o.Customer).Include(o => o.Store).Include(o => o.Service)
            .Where(o => storeIds.Contains(o.StoreId)).AsNoTracking().ToListAsync();

        decimal Total(Order o) => o.ActualTotal > 0 ? o.ActualTotal : o.TotalEstimate;
        return new StoreOwnerDashboardVm
        {
            MyStores = stores,
            TotalOrdersToday = orders.Count(o => o.CreatedAt.Date == today),
            PendingConfirmation = orders.Count(o => o.Status == OrderStatus.PickupFeePaid),
            Processing = orders.Count(o => o.Status == OrderStatus.Processing),
            Ready = orders.Count(o => o.Status == OrderStatus.Ready),
            RevenueToday = orders.Where(o => o.CreatedAt.Date == today && o.PaymentState == PaymentState.FullyPaid).Sum(Total),
            RecentOrders = orders.OrderByDescending(o => o.CreatedAt).Take(20).ToList()
        };
    }
}

// ═══════════════════════════════════════════════════════════════════
// Order, store, rider, dispute, review, payment, config repos
// ═══════════════════════════════════════════════════════════════════
public interface IOrderRepo
{
    Task<List<Order>> GetAllAsync(OrderStatus? status, string? search, int page = 1, int size = 25);
    Task<List<Order>> GetByOwnerAsync(Guid ownerId, OrderStatus? status, int page = 1, int size = 25);
    Task<Order?> GetByIdAsync(Guid id);
    Task<bool> UpdateStatusAsync(Guid id, OrderStatus newStatus, string? note, Guid changedBy);
    Task<bool> AssignRiderAsync(Guid orderId, Guid riderId, Guid adminId);
    /// <summary>Assigns a delivery rider to the order (separate from the pickup rider).</summary>
    Task<bool> AssignDeliveryRiderAsync(Guid orderId, Guid riderId, Guid adminId);
    Task<bool> OverridePriceAsync(Guid orderId, decimal newCost, string reason, Guid adminId);
    Task<int> GetTotalCountAsync(OrderStatus? status, string? search);
}

public class OrderRepo : IOrderRepo
{
    private readonly AdminDbContext _db;
    public OrderRepo(AdminDbContext db) => _db = db;

    public async Task<List<Order>> GetAllAsync(OrderStatus? status, string? search, int page = 1, int size = 25)
    {
        var q = _db.Orders.Include(o => o.Customer).Include(o => o.Store)
            .Include(o => o.Service).Include(o => o.Rider).ThenInclude(r => r!.User).AsQueryable();

        if (status.HasValue) q = q.Where(o => o.Status == status.Value);
        if (!string.IsNullOrEmpty(search))
            q = q.Where(o => o.OrderNumber.Contains(search)
                || o.Customer.FullName.Contains(search)
                || o.Customer.Phone.Contains(search)
                || o.Store.Name.Contains(search));

        return await q.OrderByDescending(o => o.CreatedAt)
            .Skip((page - 1) * size).Take(size).AsNoTracking().ToListAsync();
    }

    public async Task<List<Order>> GetByOwnerAsync(Guid ownerId, OrderStatus? status, int page = 1, int size = 25)
    {
        var storeIds = await _db.Stores.Where(s => s.OwnerId == ownerId).Select(s => s.Id).ToListAsync();
        var q = _db.Orders.Include(o => o.Customer).Include(o => o.Store).Include(o => o.Service)
            .Include(o => o.Rider).ThenInclude(r => r!.User)
            .Where(o => storeIds.Contains(o.StoreId));
        if (status.HasValue) q = q.Where(o => o.Status == status.Value);
        return await q.OrderByDescending(o => o.CreatedAt)
            .Skip((page - 1) * size).Take(size).AsNoTracking().ToListAsync();
    }

    public Task<Order?> GetByIdAsync(Guid id) =>
        _db.Orders.Include(o => o.Customer).Include(o => o.Store).Include(o => o.Service)
            .Include(o => o.Rider).ThenInclude(r => r!.User)
            .Include(o => o.DeliveryRider).ThenInclude(r => r!.User)
            .Include(o => o.StatusHistory)
            .Include(o => o.Payments)
            .Include(o => o.Review).Include(o => o.Dispute)
            .FirstOrDefaultAsync(o => o.Id == id);

    public async Task<bool> UpdateStatusAsync(Guid id, OrderStatus newStatus, string? note, Guid changedBy)
    {
        var o = await _db.Orders.FindAsync(id);
        if (o == null) return false;
        o.Status = newStatus; o.UpdatedAt = DateTime.UtcNow;
        var now = DateTime.UtcNow;
        switch (newStatus)
        {
            case OrderStatus.PickupFeePaid: o.PickupFeePaidAt = now; o.PaymentState = PaymentState.PickupFeePaid; break;
            case OrderStatus.PickedUp: o.PickedUpAt = now; break;
            case OrderStatus.Processing: o.ProcessingStartedAt = now; break;
            case OrderStatus.Ready: o.ReadyAt = now; break;
            case OrderStatus.OutForDelivery: o.OutForDeliveryAt = now; break;
            case OrderStatus.Delivered: o.DeliveredAt = now; o.PaymentState = PaymentState.BalancePending; break;
            case OrderStatus.Completed: o.CompletedAt = now; o.PaymentState = PaymentState.FullyPaid; break;
        }
        _db.OrderStatusHistories.Add(new OrderStatusHistory
        {
            OrderId = id, Status = newStatus, Note = note, ChangedByUserId = changedBy
        });
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> AssignRiderAsync(Guid orderId, Guid riderId, Guid adminId)
    {
        var o = await _db.Orders.FindAsync(orderId);
        var rider = await _db.Riders.Include(r => r.User).FirstOrDefaultAsync(r => r.Id == riderId);
        if (o == null || rider == null) return false;
        o.RiderId = riderId; o.RiderAssignedAt = DateTime.UtcNow;
        if (o.Status == OrderStatus.PickupFeePaid || o.Status == OrderStatus.RiderAssigned)
            o.Status = OrderStatus.PickUpRiderAssigned;
        o.UpdatedAt = DateTime.UtcNow;
        _db.OrderStatusHistories.Add(new OrderStatusHistory
        {
            OrderId = orderId, Status = OrderStatus.PickUpRiderAssigned,
            Note = $"Pickup rider {rider.User.FullName} assigned by admin", ChangedByUserId = adminId
        });
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> AssignDeliveryRiderAsync(Guid orderId, Guid riderId, Guid adminId)
    {
        var o = await _db.Orders.FindAsync(orderId);
        var rider = await _db.Riders.Include(r => r.User).FirstOrDefaultAsync(r => r.Id == riderId);
        if (o == null || rider == null) return false;
        o.DeliveryRiderId = riderId; o.UpdatedAt = DateTime.UtcNow;
        if (o.Status == OrderStatus.Ready || o.Status == OrderStatus.DeliveryRiderAssigned)
        {
            o.Status = OrderStatus.DeliveryRiderAssigned;
            _db.OrderStatusHistories.Add(new OrderStatusHistory
            {
                OrderId = orderId, Status = OrderStatus.DeliveryRiderAssigned,
                Note = $"Delivery rider {rider.User.FullName} assigned by admin",
                ChangedByUserId = adminId
            });
        }
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> OverridePriceAsync(Guid orderId, decimal newCost, string reason, Guid adminId)
    {
        var o = await _db.Orders.FindAsync(orderId);
        if (o == null) return false;
        o.ActualLaundryCost = newCost;
        o.ActualTotal = newCost + o.PickupFee + o.DeliveryFee;
        o.BalanceAmount = newCost + o.DeliveryFee;
        o.AdminPriceOverride = true;
        o.AdminOverrideReason = reason;
        o.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return true;
    }

    public Task<int> GetTotalCountAsync(OrderStatus? status, string? search)
    {
        var q = _db.Orders.AsQueryable();
        if (status.HasValue) q = q.Where(o => o.Status == status.Value);
        if (!string.IsNullOrEmpty(search))
            q = q.Where(o => o.OrderNumber.Contains(search) || o.Customer.FullName.Contains(search));
        return q.CountAsync();
    }
}

public interface IStoreRepo
{
    Task<List<LaundryStore>> GetAllAsync();
    Task<List<LaundryStore>> GetByOwnerAsync(Guid ownerId);
    Task<LaundryStore?> GetByIdAsync(Guid id);
    Task<bool> UpdateStatusAsync(Guid id, StoreStatus status);
    Task<bool> ToggleVerifiedAsync(Guid id);
    Task<bool> ToggleTopRatedAsync(Guid id);
    Task<bool> UpdateProfileAsync(LaundryStore updated);
    /// <summary>Manually opens or closes a store (sets IsManuallyClosed).</summary>
    Task<bool> ToggleManualCloseAsync(Guid id, bool isClosed);
}

public class StoreRepo : IStoreRepo
{
    private readonly AdminDbContext _db;
    public StoreRepo(AdminDbContext db) => _db = db;

    public Task<List<LaundryStore>> GetAllAsync() =>
        _db.Stores.Include(s => s.Owner).Include(s => s.Services)
            .OrderByDescending(s => s.CreatedAt).AsNoTracking().ToListAsync();

    public Task<List<LaundryStore>> GetByOwnerAsync(Guid ownerId) =>
        _db.Stores.Include(s => s.Services).Where(s => s.OwnerId == ownerId)
            .AsNoTracking().ToListAsync();

    public Task<LaundryStore?> GetByIdAsync(Guid id) =>
        _db.Stores.Include(s => s.Owner).Include(s => s.Services).Include(s => s.Reviews)
            .ThenInclude(r => r.Customer)
            .FirstOrDefaultAsync(s => s.Id == id);

    public async Task<bool> UpdateStatusAsync(Guid id, StoreStatus status)
    {
        var s = await _db.Stores.FindAsync(id);
        if (s == null) return false;
        s.Status = status; s.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ToggleVerifiedAsync(Guid id)
    {
        var s = await _db.Stores.FindAsync(id);
        if (s == null) return false;
        s.IsVerified = !s.IsVerified; s.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ToggleTopRatedAsync(Guid id)
    {
        var s = await _db.Stores.FindAsync(id);
        if (s == null) return false;
        s.IsTopRated = !s.IsTopRated; s.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> UpdateProfileAsync(LaundryStore updated)
    {
        var s = await _db.Stores.FindAsync(updated.Id);
        if (s == null) return false;
        s.Name = updated.Name; s.Description = updated.Description;
        s.Phone = updated.Phone; s.Email = updated.Email;
        s.Address = updated.Address; s.Area = updated.Area;
        s.OpeningHours = updated.OpeningHours;
        s.PickupFee = updated.PickupFee; s.DeliveryFee = updated.DeliveryFee;
        s.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ToggleManualCloseAsync(Guid id, bool isClosed)
    {
        var s = await _db.Stores.FindAsync(id);
        if (s == null) return false;
        s.IsManuallyClosed = isClosed;
        if (isClosed) s.LastClosedAt = DateTime.UtcNow;
        else          s.LastOpenedAt = DateTime.UtcNow;
        s.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return true;
    }
}

public interface IRiderRepo
{
    Task<List<Rider>> GetAllAsync();
    Task<List<Rider>> GetPendingApprovalAsync();
    Task<List<Rider>> GetApprovedAsync();
    Task<Rider?> GetByIdAsync(Guid id);
    Task<bool> CreateAsync(string fullName, string phone, string vehicleType, string? plate,
        string? profilePhoto, Guid createdByUserId);
    Task<bool> ToggleOnlineAsync(Guid id);
}

public class RiderRepo : IRiderRepo
{
    private readonly AdminDbContext _db;
    public RiderRepo(AdminDbContext db) => _db = db;

    public Task<List<Rider>> GetAllAsync() =>
        _db.Riders.Include(r => r.User).Include(r => r.Orders)
            .OrderByDescending(r => r.User.IsActive).ThenByDescending(r => r.TotalDeliveries)
            .AsNoTracking().ToListAsync();

    public Task<List<Rider>> GetPendingApprovalAsync() =>
        _db.Riders.Include(r => r.User)
            .Where(r => r.ApprovalStatus == ApprovalStatus.Pending)
            .OrderByDescending(r => r.CreatedAt).AsNoTracking().ToListAsync();

    public Task<List<Rider>> GetApprovedAsync() =>
        _db.Riders.Include(r => r.User)
            .Where(r => r.ApprovalStatus == ApprovalStatus.Approved && r.User.IsActive)
            .OrderBy(r => r.User.FullName).AsNoTracking().ToListAsync();

    public Task<Rider?> GetByIdAsync(Guid id) =>
        _db.Riders.Include(r => r.User).Include(r => r.Orders).ThenInclude(o => o.Customer)
            .FirstOrDefaultAsync(r => r.Id == id);

    public async Task<bool> CreateAsync(string fullName, string phone, string vehicleType,
        string? plate, string? profilePhoto, Guid createdByUserId)
    {
        if (await _db.Users.AnyAsync(u => u.Phone == phone)) return false;
        var user = new User
        {
            Phone = phone, FullName = fullName,
            Role = UserRole.Rider, IsActive = false,  // inactive until approved
            ProfilePhoto = profilePhoto
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        _db.Riders.Add(new Rider
        {
            UserId = user.Id,
            VehicleType = vehicleType,
            VehiclePlate = plate,
            Status = RiderStatus.Pending,
            ApprovalStatus = ApprovalStatus.Pending,
            CreatedByUserId = createdByUserId
        });
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ToggleOnlineAsync(Guid id)
    {
        var r = await _db.Riders.FindAsync(id);
        if (r == null) return false;
        r.Status = r.Status == RiderStatus.OnlineAvailable ? RiderStatus.Offline : RiderStatus.OnlineAvailable;
        await _db.SaveChangesAsync();
        return true;
    }
}

public interface IDisputeRepo
{
    Task<List<Dispute>> GetAllAsync(DisputeStatus? status = null);
    Task<List<Dispute>> GetByOwnerStoresAsync(Guid ownerId);
    Task<Dispute?> GetByIdAsync(Guid id);
    Task<bool> UpdateStatusAsync(Guid id, DisputeStatus status, string? note);
    Task<bool> AddMessageAsync(Guid disputeId, Guid senderId, string message, bool isAdmin);
    Task<bool> ResolveAsync(Guid id, DisputeResolution resolution, string note, decimal? refundAmount);
}

public class DisputeRepo : IDisputeRepo
{
    private readonly AdminDbContext _db;
    public DisputeRepo(AdminDbContext db) => _db = db;

    public Task<List<Dispute>> GetAllAsync(DisputeStatus? status)
    {
        var q = _db.Disputes.Include(d => d.Order).ThenInclude(o => o.Store)
            .Include(d => d.Order).ThenInclude(o => o.Customer)
            .Include(d => d.RaisedBy).AsQueryable();
        if (status.HasValue) q = q.Where(d => d.Status == status.Value);
        return q.OrderByDescending(d => d.CreatedAt).AsNoTracking().ToListAsync();
    }

    public Task<List<Dispute>> GetByOwnerStoresAsync(Guid ownerId)
    {
        var storeIds = _db.Stores.Where(s => s.OwnerId == ownerId).Select(s => s.Id);
        return _db.Disputes.Include(d => d.Order).ThenInclude(o => o.Store)
            .Include(d => d.Order).ThenInclude(o => o.Customer).Include(d => d.RaisedBy)
            .Where(d => storeIds.Contains(d.Order.StoreId))
            .OrderByDescending(d => d.CreatedAt).AsNoTracking().ToListAsync();
    }

    public Task<Dispute?> GetByIdAsync(Guid id) =>
        _db.Disputes.Include(d => d.Order).ThenInclude(o => o.Customer)
            .Include(d => d.Order).ThenInclude(o => o.Store)
            .Include(d => d.RaisedBy)
            .Include(d => d.Messages).ThenInclude(m => m.Sender)
            .FirstOrDefaultAsync(d => d.Id == id);

    public async Task<bool> UpdateStatusAsync(Guid id, DisputeStatus status, string? note)
    {
        var d = await _db.Disputes.FindAsync(id);
        if (d == null) return false;
        d.Status = status; d.UpdatedAt = DateTime.UtcNow;
        if (note != null)
            d.AdminNotes = (d.AdminNotes ?? "") + $"\n[{DateTime.UtcNow:MMM dd HH:mm}] {note}";
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> AddMessageAsync(Guid disputeId, Guid senderId, string message, bool isAdmin)
    {
        _db.DisputeMessages.Add(new DisputeMessage
        {
            DisputeId = disputeId, SenderId = senderId, Message = message, IsAdminMessage = isAdmin
        });
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ResolveAsync(Guid id, DisputeResolution resolution, string note, decimal? refundAmount)
    {
        var d = await _db.Disputes.Include(x => x.Order).FirstOrDefaultAsync(x => x.Id == id);
        if (d == null) return false;
        d.Resolution = resolution; d.ResolutionNote = note;
        d.RefundAmount = refundAmount;
        d.ResolvedAt = DateTime.UtcNow; d.UpdatedAt = DateTime.UtcNow;
        d.Status = resolution is DisputeResolution.Refund or DisputeResolution.PartialRefund
            ? DisputeStatus.Refunded : DisputeStatus.Resolved;
        if (d.Status == DisputeStatus.Refunded)
            d.Order.PaymentState = PaymentState.Refunded;
        await _db.SaveChangesAsync();
        return true;
    }
}

public interface IReviewRepo
{
    Task<List<Review>> GetAllAsync();
    Task<List<Review>> GetByOwnerStoresAsync(Guid ownerId);
    Task<bool> ModerateAsync(Guid id, bool visible, string? note);
}

public class ReviewRepo : IReviewRepo
{
    private readonly AdminDbContext _db;
    public ReviewRepo(AdminDbContext db) => _db = db;

    public Task<List<Review>> GetAllAsync() =>
        _db.Reviews.Include(r => r.Customer).Include(r => r.Store).Include(r => r.Order)
            .OrderByDescending(r => r.CreatedAt).AsNoTracking().ToListAsync();

    public Task<List<Review>> GetByOwnerStoresAsync(Guid ownerId)
    {
        var storeIds = _db.Stores.Where(s => s.OwnerId == ownerId).Select(s => s.Id);
        return _db.Reviews.Include(r => r.Customer).Include(r => r.Store).Include(r => r.Order)
            .Where(r => storeIds.Contains(r.StoreId))
            .OrderByDescending(r => r.CreatedAt).AsNoTracking().ToListAsync();
    }

    public async Task<bool> ModerateAsync(Guid id, bool visible, string? note)
    {
        var r = await _db.Reviews.FindAsync(id);
        if (r == null) return false;
        r.IsVisible = visible; r.IsFlagged = !visible; r.AdminNote = note;
        await _db.SaveChangesAsync();
        return true;
    }
}

public interface IPaymentRepo
{
    Task<List<Payment>> GetAllAsync(int page = 1, int size = 30);
}

public class PaymentRepo : IPaymentRepo
{
    private readonly AdminDbContext _db;
    public PaymentRepo(AdminDbContext db) => _db = db;

    public Task<List<Payment>> GetAllAsync(int page = 1, int size = 30) =>
        _db.Payments.Include(p => p.Order).ThenInclude(o => o.Customer)
            .Include(p => p.Order).ThenInclude(o => o.Store)
            .OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * size).Take(size).AsNoTracking().ToListAsync();
}

public interface IConfigRepo
{
    Task<List<SystemConfig>> GetAllAsync();
    Task<bool> UpdateAsync(string key, string value, Guid adminId);
}

public class ConfigRepo : IConfigRepo
{
    private readonly AdminDbContext _db;
    public ConfigRepo(AdminDbContext db) => _db = db;

    public Task<List<SystemConfig>> GetAllAsync() =>
        _db.SystemConfigs.OrderBy(c => c.Key).AsNoTracking().ToListAsync();

    public async Task<bool> UpdateAsync(string key, string value, Guid adminId)
    {
        var c = await _db.SystemConfigs.FirstOrDefaultAsync(x => x.Key == key);
        if (c == null)
        {
            _db.SystemConfigs.Add(new API.Models.SystemConfig { Key = key, Value = value, UpdatedAt = DateTime.UtcNow, UpdatedByUserId = adminId });
        }
        else
        {
            c.Value = value; c.UpdatedAt = DateTime.UtcNow; c.UpdatedByUserId = adminId;
        }
        await _db.SaveChangesAsync();
        return true;
    }
}

// ═══════════════════════════════════════════════════════════════════
// Store extensions — Create and Approve with validation
// ═══════════════════════════════════════════════════════════════════
public interface IStoreExtendedRepo
{
    Task<Guid> CreateAsync(LaundryStore store);
    Task<(bool Success, string? Error)> ApproveAsync(Guid id, Guid adminId);
    Task<List<User>> GetStoreOwnersAsync();
}

public class StoreExtendedRepo : IStoreExtendedRepo
{
    private readonly AdminDbContext _db;
    public StoreExtendedRepo(AdminDbContext db) => _db = db;

    public async Task<Guid> CreateAsync(LaundryStore store)
    {
        _db.Stores.Add(store);
        await _db.SaveChangesAsync();
        return store.Id;
    }

    public async Task<(bool Success, string? Error)> ApproveAsync(Guid id, Guid adminId)
    {
        var store = await _db.Stores
            .Include(s => s.StoreImages)
            .Include(s => s.Services)
            .FirstOrDefaultAsync(s => s.Id == id);
        if (store == null) return (false, "Store not found.");
        if (!store.StoreImages.Any())
            return (false, "Store cannot be activated because it has no uploaded image.");
        if (string.IsNullOrWhiteSpace(store.Phone))
            return (false, "Store cannot be activated because contact phone is missing.");
        if (string.IsNullOrWhiteSpace(store.Address))
            return (false, "Store cannot be activated because address is missing.");
        store.Status = StoreStatus.Active;
        store.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return (true, null);
    }

    public Task<List<User>> GetStoreOwnersAsync() =>
        _db.Users.Where(u => u.Role == UserRole.StoreOwner && u.IsActive)
            .OrderBy(u => u.FullName).AsNoTracking().ToListAsync();
}

// ═══════════════════════════════════════════════════════════════════
// Service Offerings CRUD
// ═══════════════════════════════════════════════════════════════════
public interface IServiceOfferingRepo
{
    Task<List<ServiceOffering>> GetAllAsync(Guid? storeId = null);
    Task<List<ServiceOffering>> GetByStoreAsync(Guid storeId);
    Task<ServiceOffering?> GetByIdAsync(Guid id);
    Task<bool> CreateAsync(ServiceOffering offering);
    Task<bool> UpdateAsync(ServiceOffering updated);
    Task<bool> ToggleActiveAsync(Guid id);
}

public class ServiceOfferingRepo : IServiceOfferingRepo
{
    private readonly AdminDbContext _db;
    public ServiceOfferingRepo(AdminDbContext db) => _db = db;

    public Task<List<ServiceOffering>> GetAllAsync(Guid? storeId = null)
    {
        var q = _db.ServiceOfferings.Include(s => s.Store).AsQueryable();
        if (storeId.HasValue) q = q.Where(s => s.StoreId == storeId.Value);
        return q.OrderBy(s => s.Store.Name).ThenBy(s => s.Name).AsNoTracking().ToListAsync();
    }

    public Task<List<ServiceOffering>> GetByStoreAsync(Guid storeId) =>
        _db.ServiceOfferings.Where(s => s.StoreId == storeId)
            .OrderBy(s => s.Name).AsNoTracking().ToListAsync();

    public Task<ServiceOffering?> GetByIdAsync(Guid id) =>
        _db.ServiceOfferings.Include(s => s.Store).FirstOrDefaultAsync(s => s.Id == id);

    public async Task<bool> CreateAsync(ServiceOffering offering)
    {
        offering.CreatedAt = DateTime.UtcNow;
        offering.UpdatedAt = DateTime.UtcNow;
        _db.ServiceOfferings.Add(offering);
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> UpdateAsync(ServiceOffering updated)
    {
        var s = await _db.ServiceOfferings.FindAsync(updated.Id);
        if (s == null) return false;
        s.Name = updated.Name; s.Description = updated.Description;
        s.Category = updated.Category; s.ServiceType = updated.ServiceType;
        s.PricingMode = updated.PricingMode; s.BasePrice = updated.BasePrice;
        s.MinimumCharge = updated.MinimumCharge; s.EstimatedHours = updated.EstimatedHours;
        s.IsActive = updated.IsActive; s.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ToggleActiveAsync(Guid id)
    {
        var s = await _db.ServiceOfferings.FindAsync(id);
        if (s == null) return false;
        s.IsActive = !s.IsActive; s.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return true;
    }
}

// ═══════════════════════════════════════════════════════════════════
// Store Images
// ═══════════════════════════════════════════════════════════════════
public interface IStoreImageRepo
{
    Task<List<API.Models.StoreImage>> GetByStoreAsync(Guid storeId);
    Task<Guid> AddAsync(API.Models.StoreImage image);
    Task<bool> SetPrimaryAsync(Guid imageId, Guid storeId);
    Task<(bool Success, string? Error)> DeleteAsync(Guid imageId, Guid storeId);
}

public class StoreImageRepo : IStoreImageRepo
{
    private readonly AdminDbContext _db;
    public StoreImageRepo(AdminDbContext db) => _db = db;

    public Task<List<API.Models.StoreImage>> GetByStoreAsync(Guid storeId) =>
        _db.StoreImages.Where(i => i.StoreId == storeId)
            .OrderByDescending(i => i.IsPrimary).ThenByDescending(i => i.UploadedAt)
            .AsNoTracking().ToListAsync();

    public async Task<Guid> AddAsync(API.Models.StoreImage image)
    {
        var hasPrimary = await _db.StoreImages.AnyAsync(i => i.StoreId == image.StoreId);
        if (!hasPrimary)
        {
            image.IsPrimary = true;
            // First image for this store → also set as LogoUrl on the store entity
            var store = await _db.Stores.FindAsync(image.StoreId);
            if (store != null && string.IsNullOrEmpty(store.LogoUrl))
            {
                store.LogoUrl = image.ImagePath;
                store.UpdatedAt = DateTime.UtcNow;
            }
        }
        _db.StoreImages.Add(image);
        await _db.SaveChangesAsync();
        return image.Id;
    }

    public async Task<bool> SetPrimaryAsync(Guid imageId, Guid storeId)
    {
        var images = await _db.StoreImages.Where(i => i.StoreId == storeId).ToListAsync();
        foreach (var img in images) img.IsPrimary = img.Id == imageId;

        // Sync LogoUrl on the store to the newly-selected primary image
        var primary = images.FirstOrDefault(i => i.Id == imageId);
        if (primary != null)
        {
            var store = await _db.Stores.FindAsync(storeId);
            if (store != null)
            {
                store.LogoUrl = primary.ImagePath;
                store.UpdatedAt = DateTime.UtcNow;
            }
        }
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<(bool Success, string? Error)> DeleteAsync(Guid imageId, Guid storeId)
    {
        var image = await _db.StoreImages.FirstOrDefaultAsync(i => i.Id == imageId && i.StoreId == storeId);
        if (image == null) return (false, "Image not found.");
        var store = await _db.Stores.FirstOrDefaultAsync(s => s.Id == storeId);
        var imageCount = await _db.StoreImages.CountAsync(i => i.StoreId == storeId);
        if (imageCount == 1 && store?.Status == StoreStatus.Active)
            return (false, "Cannot delete the only image of an active store. Upload another image first.");
        _db.StoreImages.Remove(image);
        if (image.IsPrimary && imageCount > 1)
        {
            var next = await _db.StoreImages.FirstOrDefaultAsync(i => i.StoreId == storeId && i.Id != imageId);
            if (next != null) next.IsPrimary = true;
        }
        await _db.SaveChangesAsync();
        return (true, null);
    }
}

// ═══════════════════════════════════════════════════════════════════
// Rider extended — approve, reject, edit, status
// ═══════════════════════════════════════════════════════════════════
public interface IRiderExtendedRepo
{
    Task<bool> UpdateAsync(Rider rider, User user);
    /// <summary>
    /// Approves a rider. Enforces segregation of duties:
    /// the admin who created/registered the rider CANNOT approve them.
    /// </summary>
    Task<(bool Success, string? Error)> ApproveAsync(Guid id, Guid adminId);
    /// <summary>Rejects a pending rider with a required reason.</summary>
    Task<(bool Success, string? Error)> RejectAsync(Guid id, Guid adminId, string reason);
    Task<bool> UpdateStatusAsync(Guid id, RiderStatus status);
}

public class RiderExtendedRepo : IRiderExtendedRepo
{
    private readonly AdminDbContext _db;
    public RiderExtendedRepo(AdminDbContext db) => _db = db;

    public async Task<bool> UpdateAsync(Rider rider, User user)
    {
        var r = await _db.Riders.FindAsync(rider.Id);
        var u = await _db.Users.FindAsync(rider.UserId);
        if (r == null || u == null) return false;
        u.FullName = user.FullName;
        if (!string.IsNullOrEmpty(user.ProfilePhoto)) u.ProfilePhoto = user.ProfilePhoto;
        if (!string.IsNullOrEmpty(user.ProfileImageFileName))
        {
            u.ProfileImageFileName = user.ProfileImageFileName;
            u.ProfileImageContentType = user.ProfileImageContentType;
            u.ProfileImageSize = user.ProfileImageSize;
            u.ProfileImageUpdatedAt = DateTime.UtcNow;
        }
        r.VehicleType = rider.VehicleType;
        r.VehiclePlate = rider.VehiclePlate;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<(bool Success, string? Error)> ApproveAsync(Guid id, Guid adminId)
    {
        var rider = await _db.Riders.Include(r => r.User).FirstOrDefaultAsync(r => r.Id == id);
        if (rider == null) return (false, "Rider not found.");

        // ── Segregation of duties: creator cannot approve ─────────
        if (rider.CreatedByUserId.HasValue && rider.CreatedByUserId.Value == adminId)
            return (false, "You cannot approve a rider you registered. This action must be taken by a different admin.");

        if (rider.ApprovalStatus == ApprovalStatus.Approved)
            return (false, "Rider is already approved.");

        if (string.IsNullOrEmpty(rider.User.ProfilePhoto))
            return (false, "Rider cannot be approved because profile picture is required.");

        rider.IsApproved = true;
        rider.ApprovalStatus = ApprovalStatus.Approved;
        rider.ApprovedAt = DateTime.UtcNow;
        rider.ApprovedByAdminId = adminId;
        rider.Status = RiderStatus.Offline;
        rider.User.IsActive = true;
        await _db.SaveChangesAsync();
        return (true, null);
    }

    public async Task<(bool Success, string? Error)> RejectAsync(Guid id, Guid adminId, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason)) return (false, "Rejection reason is required.");
        var rider = await _db.Riders.Include(r => r.User).FirstOrDefaultAsync(r => r.Id == id);
        if (rider == null) return (false, "Rider not found.");
        if (rider.ApprovalStatus == ApprovalStatus.Rejected) return (false, "Rider is already rejected.");

        // Segregation also applies to rejection
        if (rider.CreatedByUserId.HasValue && rider.CreatedByUserId.Value == adminId)
            return (false, "You cannot reject a rider you registered. This action must be taken by a different admin.");

        rider.ApprovalStatus = ApprovalStatus.Rejected;
        rider.IsApproved = false;
        rider.RejectedByUserId = adminId;
        rider.RejectedAt = DateTime.UtcNow;
        rider.RejectionReason = reason;
        rider.Status = RiderStatus.Rejected;
        rider.User.IsActive = false;
        await _db.SaveChangesAsync();
        return (true, null);
    }

    public async Task<bool> UpdateStatusAsync(Guid id, RiderStatus status)
    {
        var r = await _db.Riders.FindAsync(id);
        if (r == null) return false;
        r.Status = status;
        await _db.SaveChangesAsync();
        return true;
    }
}

// ═══════════════════════════════════════════════════════════════════
// User profile and password management
// ═══════════════════════════════════════════════════════════════════
public interface IUserRepo
{
    Task<User?> GetByIdAsync(Guid id);
    Task<bool> UpdateProfileAsync(User updated);
    Task<(bool Success, string? Error)> ChangePasswordAsync(Guid userId, string currentPassword, string newPassword);
}

public class UserRepo : IUserRepo
{
    private readonly AdminDbContext _db;
    public UserRepo(AdminDbContext db) => _db = db;

    public Task<User?> GetByIdAsync(Guid id) => _db.Users.FindAsync(id).AsTask();

    public async Task<bool> UpdateProfileAsync(User updated)
    {
        var u = await _db.Users.FindAsync(updated.Id);
        if (u == null) return false;
        u.FullName = updated.FullName;
        u.Email = updated.Email;
        u.CountryCode = updated.CountryCode;
        u.LocalPhone = updated.LocalPhone;
        if (!string.IsNullOrEmpty(updated.ProfilePhoto))
        {
            u.ProfilePhoto = updated.ProfilePhoto;
            u.ProfileImageFileName = updated.ProfileImageFileName;
            u.ProfileImageContentType = updated.ProfileImageContentType;
            u.ProfileImageSize = updated.ProfileImageSize;
            u.ProfileImageUpdatedAt = DateTime.UtcNow;
        }
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<(bool Success, string? Error)> ChangePasswordAsync(Guid userId, string currentPassword, string newPassword)
    {
        var u = await _db.Users.FindAsync(userId);
        if (u == null) return (false, "User not found.");
        if (!string.IsNullOrEmpty(u.PasswordHash))
        {
            if (!Helpers.FileUploadHelper.VerifyPassword(u, u.PasswordHash, currentPassword))
                return (false, "Current password is incorrect.");
        }
        else
        {
            // Legacy check — hard-coded bootstrap credentials
            bool legacyOk = (u.Role == UserRole.Admin && currentPassword == "Vyron@Admin2024!")
                || (u.Role == UserRole.StoreOwner && currentPassword == "StoreOwner@2024");
            if (!legacyOk) return (false, "Current password is incorrect.");
        }
        u.PasswordHash = Helpers.FileUploadHelper.HashPassword(u, newPassword);
        u.PasswordChangedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return (true, null);
    }
}

// ═══════════════════════════════════════════════════════════════════
// Password reset tokens (forgot password flow)
// ═══════════════════════════════════════════════════════════════════
public interface IPasswordResetRepo
{
    Task<(string RawToken, PasswordResetToken Record)> CreateTokenAsync(Guid userId);
    Task<PasswordResetToken?> GetValidByTokenAsync(string rawToken);
    Task<bool> ResetPasswordAsync(Guid tokenId, Guid userId, string newPassword);
}

public class PasswordResetRepo : IPasswordResetRepo
{
    private readonly AdminDbContext _db;
    public PasswordResetRepo(AdminDbContext db) => _db = db;

    public async Task<(string RawToken, PasswordResetToken Record)> CreateTokenAsync(Guid userId)
    {
        // Invalidate any prior unused tokens for this user
        var old = _db.PasswordResetTokens.Where(t => t.UserId == userId && t.UsedAt == null);
        _db.PasswordResetTokens.RemoveRange(old);

        var (raw, hash) = Helpers.FileUploadHelper.GenerateResetToken();
        var record = new PasswordResetToken
        {
            UserId = userId,
            TokenHash = hash,
            ExpiresAt = DateTime.UtcNow.AddMinutes(30)
        };
        _db.PasswordResetTokens.Add(record);
        await _db.SaveChangesAsync();
        return (raw, record);
    }

    public async Task<PasswordResetToken?> GetValidByTokenAsync(string rawToken)
    {
        var hash = Helpers.FileUploadHelper.HashToken(rawToken);
        return await _db.PasswordResetTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.TokenHash == hash && t.UsedAt == null && t.ExpiresAt > DateTime.UtcNow);
    }

    public async Task<bool> ResetPasswordAsync(Guid tokenId, Guid userId, string newPassword)
    {
        var token = await _db.PasswordResetTokens.FindAsync(tokenId);
        var user = await _db.Users.FindAsync(userId);
        if (token == null || user == null) return false;
        user.PasswordHash = Helpers.FileUploadHelper.HashPassword(user, newPassword);
        user.PasswordChangedAt = DateTime.UtcNow;
        token.UsedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return true;
    }
}

// ═══════════════════════════════════════════════════════════════════
// Admin User Management (SuperAdmin only)
// ═══════════════════════════════════════════════════════════════════
public interface IAdminUserRepo
{
    Task<List<User>> GetAllAdminUsersAsync();
    Task<User?> GetByIdAsync(Guid id);
    /// <summary>Creates an Admin or AdminUser account. Returns (id, error).</summary>
    Task<(Guid? Id, string? Error)> CreateAsync(string fullName, string phone, string email, string password, UserRole role);
    Task<bool> DeactivateAsync(Guid id, Guid byUserId);
    Task<bool> ReactivateAsync(Guid id);
}

public class AdminUserRepo : IAdminUserRepo
{
    private readonly AdminDbContext _db;
    public AdminUserRepo(AdminDbContext db) => _db = db;

    public Task<List<User>> GetAllAdminUsersAsync() =>
        _db.Users.Where(u => u.Role == UserRole.Admin || u.Role == UserRole.AdminUser || u.Role == UserRole.SuperAdmin)
            .OrderBy(u => u.Role).ThenBy(u => u.FullName).AsNoTracking().ToListAsync();

    public Task<User?> GetByIdAsync(Guid id) => _db.Users.FindAsync(id).AsTask();

    public async Task<(Guid? Id, string? Error)> CreateAsync(string fullName, string phone, string email, string password, UserRole role)
    {
        if (await _db.Users.AnyAsync(u => u.Phone == phone))
            return (null, "A user with this phone number already exists.");
        if (!string.IsNullOrEmpty(email) && await _db.Users.AnyAsync(u => u.Email == email))
            return (null, "A user with this email already exists.");
        if (role is not (UserRole.Admin or UserRole.AdminUser or UserRole.SuperAdmin))
            return (null, "Only Admin, AdminUser, or SuperAdmin roles can be created here.");

        var user = new User
        {
            Phone = phone, Email = email, FullName = fullName,
            Role = role, IsActive = true,
            PasswordHash = Helpers.FileUploadHelper.HashPassword(new User(), password)
        };
        _db.Users.Add(user);

        // Assign role in UserRoles table
        var roleEntity = await _db.Roles.FirstOrDefaultAsync(r => r.NormalizedName == role.ToString().ToUpperInvariant());
        if (roleEntity != null)
            _db.UserRoles.Add(new AppUserRole { UserId = user.Id, RoleId = roleEntity.Id });

        await _db.SaveChangesAsync();
        return (user.Id, null);
    }

    public async Task<bool> DeactivateAsync(Guid id, Guid byUserId)
    {
        var u = await _db.Users.FindAsync(id);
        if (u == null) return false;
        u.IsActive = false;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ReactivateAsync(Guid id)
    {
        var u = await _db.Users.FindAsync(id);
        if (u == null) return false;
        u.IsActive = true;
        await _db.SaveChangesAsync();
        return true;
    }
}

// ═══════════════════════════════════════════════════════════════════
// StoreOwner Approval (SuperAdmin/Admin workflow)
// ═══════════════════════════════════════════════════════════════════
public interface IStoreOwnerApprovalRepo
{
    Task<List<User>> GetPendingAsync();
    Task<List<User>> GetAllApplicantsAsync();
    Task<User?> GetByIdAsync(Guid id);
    Task<(bool Success, string? Error)> ApproveAsync(Guid userId, Guid byAdminId);
    Task<(bool Success, string? Error)> RejectAsync(Guid userId, Guid byAdminId, string reason);
}

public class StoreOwnerApprovalRepo : IStoreOwnerApprovalRepo
{
    private readonly AdminDbContext _db;
    public StoreOwnerApprovalRepo(AdminDbContext db) => _db = db;

    public Task<List<User>> GetPendingAsync() =>
        _db.Users.Where(u => u.Role == UserRole.StoreOwner && u.RegistrationApprovalStatus == ApprovalStatus.Pending)
            .OrderByDescending(u => u.CreatedAt).AsNoTracking().ToListAsync();

    public Task<List<User>> GetAllApplicantsAsync() =>
        _db.Users.Where(u => u.Role == UserRole.StoreOwner && u.RegistrationApprovalStatus != null)
            .OrderByDescending(u => u.CreatedAt).AsNoTracking().ToListAsync();

    public Task<User?> GetByIdAsync(Guid id) => _db.Users.FindAsync(id).AsTask();

    public async Task<(bool Success, string? Error)> ApproveAsync(Guid userId, Guid byAdminId)
    {
        var u = await _db.Users.FindAsync(userId);
        if (u == null) return (false, "User not found.");
        if (u.Role != UserRole.StoreOwner) return (false, "User is not a StoreOwner applicant.");
        if (u.RegistrationApprovalStatus == ApprovalStatus.Approved) return (false, "Already approved.");
        u.RegistrationApprovalStatus = ApprovalStatus.Approved;
        u.ApprovedByUserId = byAdminId;
        u.RegistrationApprovedAt = DateTime.UtcNow;
        u.IsActive = true;
        await _db.SaveChangesAsync();
        return (true, null);
    }

    public async Task<(bool Success, string? Error)> RejectAsync(Guid userId, Guid byAdminId, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason)) return (false, "Rejection reason is required.");
        var u = await _db.Users.FindAsync(userId);
        if (u == null) return (false, "User not found.");
        if (u.RegistrationApprovalStatus == ApprovalStatus.Rejected) return (false, "Already rejected.");
        u.RegistrationApprovalStatus = ApprovalStatus.Rejected;
        u.RejectedByUserId = byAdminId;
        u.RegistrationRejectedAt = DateTime.UtcNow;
        u.RegistrationRejectionReason = reason;
        u.IsActive = false;
        await _db.SaveChangesAsync();
        return (true, null);
    }
}

// ═══════════════════════════════════════════════════════════════════
// Audit Log, Activity Log, Communication Log (read-only portal repos)
// ═══════════════════════════════════════════════════════════════════
public interface IAuditLogRepo
{
    Task<List<AuditLog>> GetAllAsync(string? search, int page = 1, int size = 50);
    Task<int> CountAsync(string? search);
}

public class AuditLogRepo : IAuditLogRepo
{
    private readonly AdminDbContext _db;
    public AuditLogRepo(AdminDbContext db) => _db = db;

    public Task<List<AuditLog>> GetAllAsync(string? search, int page = 1, int size = 50)
    {
        var q = _db.AuditLogs.Include(a => a.User).AsQueryable();
        if (!string.IsNullOrEmpty(search))
            q = q.Where(a => a.Action.Contains(search) || a.Entity.Contains(search)
                || (a.User != null && a.User.FullName.Contains(search)));
        return q.OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * size).Take(size).AsNoTracking().ToListAsync();
    }

    public Task<int> CountAsync(string? search)
    {
        var q = _db.AuditLogs.AsQueryable();
        if (!string.IsNullOrEmpty(search))
            q = q.Where(a => a.Action.Contains(search) || a.Entity.Contains(search));
        return q.CountAsync();
    }
}

public interface IActivityLogRepo
{
    Task<List<ActivityLog>> GetAllAsync(string? search, int page = 1, int size = 50);
    Task<int> CountAsync(string? search);
}

public class ActivityLogRepo : IActivityLogRepo
{
    private readonly AdminDbContext _db;
    public ActivityLogRepo(AdminDbContext db) => _db = db;

    public Task<List<ActivityLog>> GetAllAsync(string? search, int page = 1, int size = 50)
    {
        var q = _db.ActivityLogs.Include(a => a.User).AsQueryable();
        if (!string.IsNullOrEmpty(search))
            q = q.Where(a => a.ActivityType.Contains(search)
                || (a.Description != null && a.Description.Contains(search))
                || (a.User != null && a.User.FullName.Contains(search)));
        return q.OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * size).Take(size).AsNoTracking().ToListAsync();
    }

    public Task<int> CountAsync(string? search)
    {
        var q = _db.ActivityLogs.AsQueryable();
        if (!string.IsNullOrEmpty(search))
            q = q.Where(a => a.ActivityType.Contains(search)
                || (a.Description != null && a.Description.Contains(search)));
        return q.CountAsync();
    }
}

public interface ICommunicationLogRepo
{
    Task<List<CommunicationLog>> GetAllAsync(string? channel, string? status, int page = 1, int size = 50);
    Task<int> CountAsync(string? channel, string? status);
}

public class CommunicationLogRepo : ICommunicationLogRepo
{
    private readonly AdminDbContext _db;
    public CommunicationLogRepo(AdminDbContext db) => _db = db;

    public Task<List<CommunicationLog>> GetAllAsync(string? channel, string? status, int page = 1, int size = 50)
    {
        var q = _db.CommunicationLogs.Include(c => c.Recipient).AsQueryable();
        if (!string.IsNullOrEmpty(channel)) q = q.Where(c => c.Channel == channel);
        if (!string.IsNullOrEmpty(status)) q = q.Where(c => c.Status == status);
        return q.OrderByDescending(c => c.CreatedAt)
            .Skip((page - 1) * size).Take(size).AsNoTracking().ToListAsync();
    }

    public Task<int> CountAsync(string? channel, string? status)
    {
        var q = _db.CommunicationLogs.AsQueryable();
        if (!string.IsNullOrEmpty(channel)) q = q.Where(c => c.Channel == channel);
        if (!string.IsNullOrEmpty(status)) q = q.Where(c => c.Status == status);
        return q.CountAsync();
    }
}

// ═══════════════════════════════════════════════════════════════════
// Store Staff Management (StoreOwner assigns staff to stores)
// ═══════════════════════════════════════════════════════════════════
public interface IStoreStaffRepo
{
    Task<List<StoreUserAssignment>> GetByStoreAsync(Guid storeId);
    Task<List<StoreUserAssignment>> GetByOwnerAsync(Guid ownerId);
    Task<List<User>> GetStaffUsersAsync();
    /// <summary>Creates a new staff user and assigns them to a store.</summary>
    Task<(Guid? UserId, string? Error)> CreateStaffAsync(
        string fullName, string phone, string password, UserRole staffRole,
        Guid storeId, Guid assignedByUserId);
    Task<(bool Success, string? Error)> AssignExistingAsync(
        Guid staffUserId, Guid storeId, string staffRole, Guid assignedByUserId);
    Task<bool> RevokeAsync(Guid assignmentId, Guid revokedByUserId);
}

public class StoreStaffRepo : IStoreStaffRepo
{
    private readonly AdminDbContext _db;
    public StoreStaffRepo(AdminDbContext db) => _db = db;

    public Task<List<StoreUserAssignment>> GetByStoreAsync(Guid storeId) =>
        _db.StoreUserAssignments.Include(a => a.User).Where(a => a.StoreId == storeId && a.IsActive)
            .OrderBy(a => a.StaffRole).ThenBy(a => a.User.FullName).AsNoTracking().ToListAsync();

    public Task<List<StoreUserAssignment>> GetByOwnerAsync(Guid ownerId)
    {
        var storeIds = _db.Stores.Where(s => s.OwnerId == ownerId).Select(s => s.Id);
        return _db.StoreUserAssignments.Include(a => a.User).Include(a => a.Store)
            .Where(a => storeIds.Contains(a.StoreId) && a.IsActive)
            .OrderBy(a => a.Store.Name).ThenBy(a => a.StaffRole).AsNoTracking().ToListAsync();
    }

    public Task<List<User>> GetStaffUsersAsync() =>
        _db.Users.Where(u => u.Role == UserRole.StoreManager || u.Role == UserRole.StoreStaff)
            .OrderBy(u => u.FullName).AsNoTracking().ToListAsync();

    public async Task<(Guid? UserId, string? Error)> CreateStaffAsync(
        string fullName, string phone, string password, UserRole staffRole,
        Guid storeId, Guid assignedByUserId)
    {
        if (staffRole is not (UserRole.StoreManager or UserRole.StoreStaff))
            return (null, "Staff role must be StoreManager or StoreStaff.");
        if (await _db.Users.AnyAsync(u => u.Phone == phone))
            return (null, "A user with this phone already exists.");

        var user = new User
        {
            Phone = phone, FullName = fullName, Role = staffRole, IsActive = true,
            PasswordHash = Helpers.FileUploadHelper.HashPassword(new User(), password)
        };
        _db.Users.Add(user);
        var roleEntity = await _db.Roles.FirstOrDefaultAsync(r => r.NormalizedName == staffRole.ToString().ToUpperInvariant());
        if (roleEntity != null)
            _db.UserRoles.Add(new AppUserRole { UserId = user.Id, RoleId = roleEntity.Id });

        _db.StoreUserAssignments.Add(new StoreUserAssignment
        {
            UserId = user.Id, StoreId = storeId,
            StaffRole = staffRole.ToString(), AssignedByUserId = assignedByUserId
        });
        await _db.SaveChangesAsync();
        return (user.Id, null);
    }

    public async Task<(bool Success, string? Error)> AssignExistingAsync(
        Guid staffUserId, Guid storeId, string staffRole, Guid assignedByUserId)
    {
        var alreadyAssigned = await _db.StoreUserAssignments.AnyAsync(
            a => a.UserId == staffUserId && a.StoreId == storeId && a.IsActive);
        if (alreadyAssigned) return (false, "This staff member is already assigned to this store.");
        _db.StoreUserAssignments.Add(new StoreUserAssignment
        {
            UserId = staffUserId, StoreId = storeId,
            StaffRole = staffRole, AssignedByUserId = assignedByUserId
        });
        await _db.SaveChangesAsync();
        return (true, null);
    }

    public async Task<bool> RevokeAsync(Guid assignmentId, Guid revokedByUserId)
    {
        var a = await _db.StoreUserAssignments.FindAsync(assignmentId);
        if (a == null) return false;
        a.IsActive = false; a.RevokedAt = DateTime.UtcNow; a.RevokedByUserId = revokedByUserId;
        await _db.SaveChangesAsync();
        return true;
    }
}

// ═══════════════════════════════════════════════════════════════════
// Idle timeout action filter — sets ViewBag.IdleTimeoutMinutes
// ═══════════════════════════════════════════════════════════════════
public class IdleTimeoutFilter : Microsoft.AspNetCore.Mvc.Filters.IAsyncActionFilter
{
    private readonly AdminDbContext _db;
    private readonly Microsoft.Extensions.Caching.Memory.IMemoryCache _cache;

    public IdleTimeoutFilter(AdminDbContext db, Microsoft.Extensions.Caching.Memory.IMemoryCache cache)
    { _db = db; _cache = cache; }

    public async Task OnActionExecutionAsync(
        Microsoft.AspNetCore.Mvc.Filters.ActionExecutingContext ctx,
        Microsoft.AspNetCore.Mvc.Filters.ActionExecutionDelegate next)
    {
        if (ctx.Controller is Microsoft.AspNetCore.Mvc.Controller controller)
        {
            var timeout = await _cache.GetOrCreateAsync("IdleTimeoutMinutes", async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
                var cfg = await _db.SystemConfigs.AsNoTracking()
                    .FirstOrDefaultAsync(c => c.Key == "PortalIdleTimeoutMinutes");
                if (cfg != null && int.TryParse(cfg.Value, out var mins) && mins >= 5 && mins <= 120)
                    return mins;
                return 15;
            });
            controller.ViewBag.IdleTimeoutMinutes = timeout;
        }
        await next();
    }
}
