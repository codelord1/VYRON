using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Vyron.API.Data;
using Vyron.API.DTOs;
using Vyron.API.Hubs;
using Vyron.API.Models;
using Vyron.Shared.Enums;

namespace Vyron.API.Services;

// ═══════════════════════════════════════════════════════════════════
// STORE SERVICE
// ═══════════════════════════════════════════════════════════════════
public interface IStoreService
{
    Task<List<StoreListItemDto>> GetStoresAsync(string? search = null, string? sort = null,
        string? filter = null, double? lat = null, double? lng = null);
    Task<StoreDetailDto?> GetStoreAsync(Guid id);
    Task<LaundryStore> CreateStoreAsync(Guid ownerId, CreateStoreRequest request);
    Task<LaundryStore?> UpdateStoreAsync(Guid id, UpdateStoreRequest request);
    Task<bool> UpdateStatusAsync(Guid id, StoreStatus status, Guid adminId);
    Task<ServiceOffering> UpsertServiceAsync(Guid storeId, UpsertServiceRequest request, Guid? existingId = null);
    Task RecalculateRatingAsync(Guid storeId);
}

public class StoreService : IStoreService
{
    private readonly VyronDbContext _db;
    private readonly IAuditService _audit;

    public StoreService(VyronDbContext db, IAuditService audit)
    { _db = db; _audit = audit; }

    public async Task<List<StoreListItemDto>> GetStoresAsync(string? search, string? sort,
        string? filter, double? lat, double? lng)
    {
        var query = _db.Stores.Include(s => s.Services)
            .Where(s => s.Status == StoreStatus.Active).AsQueryable();

        if (!string.IsNullOrEmpty(search))
            query = query.Where(s => s.Name.Contains(search) || s.Area.Contains(search));

        if (filter == "verified") query = query.Where(s => s.IsVerified);
        else if (filter == "fast") query = query.Where(s => s.FastPickup);
        else if (filter == "toprated") query = query.Where(s => s.IsTopRated);

        var stores = await query.ToListAsync();

        IEnumerable<LaundryStore> sorted = sort switch
        {
            "rating" => stores.OrderByDescending(s => s.AverageRating),
            "price" => stores.OrderBy(s => s.PickupFee),
            "fast" => stores.OrderBy(s => s.EstimatedPickupMinutes),
            "distance" when lat.HasValue && lng.HasValue =>
                stores.OrderBy(s => Haversine(lat.Value, lng.Value, s.Latitude, s.Longitude)),
            _ => stores.OrderByDescending(s => s.IsVerified)
                       .ThenByDescending(s => s.AverageRating)
        };

        return sorted.Select(MapToListItem).ToList();
    }

    public async Task<StoreDetailDto?> GetStoreAsync(Guid id)
    {
        var store = await _db.Stores
            .Include(s => s.Services)
            .Include(s => s.Reviews.Where(r => r.IsVisible).OrderByDescending(r => r.CreatedAt).Take(10))
                .ThenInclude(r => r.Customer)
            .FirstOrDefaultAsync(s => s.Id == id);
        if (store == null) return null;

        return new StoreDetailDto(
            store.Id, store.Name, store.Description, store.Phone, store.Email,
            store.Address, store.Area, store.City, store.State,
            store.AverageRating, store.TotalReviews, store.TotalOrders,
            store.PickupFee, store.DeliveryFee, store.EstimatedPickupMinutes,
            store.IsVerified, store.IsTopRated, store.FastPickup,
            store.OpeningHours, store.LogoUrl, store.BannerUrl, store.Status, store.CreatedAt,
            store.Services.Where(s => s.IsActive).Select(MapService).ToList(),
            store.Reviews.Select(MapReview).ToList());
    }

    public async Task<LaundryStore> CreateStoreAsync(Guid ownerId, CreateStoreRequest req)
    {
        var store = new LaundryStore
        {
            OwnerId = ownerId, Name = req.Name, Description = req.Description,
            Phone = req.Phone, Email = req.Email, Address = req.Address, Area = req.Area,
            City = req.City, State = req.State,
            Latitude = req.Latitude, Longitude = req.Longitude,
            PickupFee = req.PickupFee, DeliveryFee = req.DeliveryFee,
            EstimatedPickupMinutes = req.EstimatedPickupMinutes,
            OpeningHours = req.OpeningHours, Status = StoreStatus.Pending
        };
        _db.Stores.Add(store);
        await _db.SaveChangesAsync();
        await _audit.LogAsync(ownerId, "STORE_CREATE", "LaundryStore", store.Id);
        return store;
    }

    public async Task<LaundryStore?> UpdateStoreAsync(Guid id, UpdateStoreRequest req)
    {
        var s = await _db.Stores.FindAsync(id);
        if (s == null) return null;
        s.Name = req.Name; s.Description = req.Description;
        s.Phone = req.Phone; s.Email = req.Email;
        s.Address = req.Address; s.Area = req.Area;
        s.PickupFee = req.PickupFee; s.DeliveryFee = req.DeliveryFee;
        s.EstimatedPickupMinutes = req.EstimatedPickupMinutes;
        s.OpeningHours = req.OpeningHours; s.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return s;
    }

    public async Task<bool> UpdateStatusAsync(Guid id, StoreStatus status, Guid adminId)
    {
        var s = await _db.Stores.FindAsync(id);
        if (s == null) return false;
        var old = s.Status.ToString();
        s.Status = status; s.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        await _audit.LogAsync(adminId, "STORE_STATUS_CHANGE", "LaundryStore", id, old, status.ToString());
        return true;
    }

    public async Task<ServiceOffering> UpsertServiceAsync(Guid storeId, UpsertServiceRequest req, Guid? existingId)
    {
        ServiceOffering svc;
        if (existingId.HasValue)
        {
            svc = await _db.ServiceOfferings.FindAsync(existingId.Value) ?? new ServiceOffering();
        }
        else
        {
            svc = new ServiceOffering { StoreId = storeId };
            _db.ServiceOfferings.Add(svc);
        }
        svc.ServiceType = req.ServiceType; svc.Name = req.Name;
        svc.Description = req.Description; svc.PricingMode = req.PricingMode;
        svc.BasePrice = req.BasePrice; svc.MinimumCharge = req.MinimumCharge;
        svc.IsActive = req.IsActive; svc.EstimatedHours = req.EstimatedHours;
        await _db.SaveChangesAsync();
        return svc;
    }

    public async Task RecalculateRatingAsync(Guid storeId)
    {
        var reviews = await _db.Reviews.Where(r => r.StoreId == storeId && r.IsVisible).ToListAsync();
        var store = await _db.Stores.FindAsync(storeId);
        if (store == null) return;
        store.TotalReviews = reviews.Count;
        store.AverageRating = reviews.Any()
            ? Math.Round((decimal)reviews.Average(r => r.Rating), 1) : 0;
        await _db.SaveChangesAsync();
    }

    private static double Haversine(double lat1, double lon1, double lat2, double lon2)
    {
        var dLat = (lat2 - lat1) * Math.PI / 180;
        var dLon = (lon2 - lon1) * Math.PI / 180;
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return 6371 * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }

    private static StoreListItemDto MapToListItem(LaundryStore s) => new(
        s.Id, s.Name, s.Description, s.Address, s.Area,
        s.AverageRating, s.TotalReviews, s.TotalOrders,
        s.PickupFee, s.DeliveryFee, s.EstimatedPickupMinutes,
        s.IsVerified, s.IsTopRated, s.FastPickup, s.Status, s.LogoUrl,
        s.Latitude, s.Longitude,
        s.Services.Where(x => x.IsActive).Select(MapService).ToList());

    private static ServiceSummaryDto MapService(ServiceOffering s) =>
        new(s.Id, s.ServiceType, s.Name, s.Description, s.PricingMode,
            s.BasePrice, s.MinimumCharge, s.IsActive, s.EstimatedHours);

    private static ReviewDto MapReview(Review r) =>
        new(r.Id, r.OrderId, "", r.Customer.FullName, r.Rating,
            r.Comment, r.PhotoUrl, r.IsVisible, r.CreatedAt);
}

// ═══════════════════════════════════════════════════════════════════
// ORDER SERVICE
// ═══════════════════════════════════════════════════════════════════
public interface IOrderService
{
    Task<PriceEstimateResponse> EstimatePriceAsync(PriceEstimateRequest req);
    Task<OrderDto> CreateOrderAsync(Guid customerId, CreateOrderRequest req);
    Task<OrderDto?> GetOrderAsync(Guid id);
    Task<OrderDto?> GetOrderByNumberAsync(string number);
    Task<List<OrderDto>> GetCustomerOrdersAsync(Guid customerId, int page = 1);
    Task<List<OrderDto>> GetAllOrdersAsync(OrderStatus? status = null, string? search = null, int page = 1);
    Task<List<OrderDto>> GetStoreOrdersAsync(Guid storeId, int page = 1);
    Task<List<OrderDto>> GetRiderOrdersAsync(Guid riderId);
    Task<OrderDto?> UpdateStatusAsync(Guid orderId, OrderStatus newStatus, string? note, Guid? changedBy);
    Task<OrderDto?> AssignRiderAsync(Guid orderId, Guid riderId, Guid adminId);
    Task<OrderDto?> OverridePriceAsync(Guid orderId, OverridePriceRequest req, Guid adminId);
}

public class OrderService : IOrderService
{
    private readonly VyronDbContext _db;
    private readonly INotificationService _notifications;
    private readonly IAuditService _audit;
    private readonly IHubContext<OrderTrackingHub> _hub;
    private readonly ILogger<OrderService> _logger;

    public OrderService(VyronDbContext db, INotificationService notifications,
        IAuditService audit, IHubContext<OrderTrackingHub> hub, ILogger<OrderService> logger)
    {
        _db = db; _notifications = notifications; _audit = audit;
        _hub = hub; _logger = logger;
    }

    public async Task<PriceEstimateResponse> EstimatePriceAsync(PriceEstimateRequest req)
    {
        var svc = await _db.ServiceOfferings.Include(s => s.Store)
            .FirstOrDefaultAsync(s => s.Id == req.ServiceOfferingId && s.IsActive)
            ?? throw new InvalidOperationException("Service not found.");

        var laundry = svc.PricingMode == PricingMode.PerKg
            ? Math.Max(req.Weight * svc.BasePrice, svc.MinimumCharge)
            : Math.Max(req.Pieces * svc.BasePrice, svc.MinimumCharge);

        var breakdown = svc.PricingMode == PricingMode.PerKg
            ? $"{svc.Name} (₦{svc.BasePrice}/kg × {req.Weight}kg)"
            : $"{svc.Name} (₦{svc.BasePrice}/item × {req.Pieces} items)";

        var total = laundry + svc.Store.PickupFee + svc.Store.DeliveryFee;
        return new PriceEstimateResponse(laundry, svc.Store.PickupFee, svc.Store.DeliveryFee, total,
            svc.Store.PickupFee, laundry + svc.Store.DeliveryFee, breakdown);
    }

    public async Task<OrderDto> CreateOrderAsync(Guid customerId, CreateOrderRequest req)
    {
        var svc = await _db.ServiceOfferings.Include(s => s.Store)
            .FirstOrDefaultAsync(s => s.Id == req.ServiceOfferingId && s.IsActive)
            ?? throw new InvalidOperationException("Service not found.");

        var estimate = await EstimatePriceAsync(new PriceEstimateRequest(req.ServiceOfferingId, req.EstimatedWeight, req.EstimatedPieces));
        var count = await _db.Orders.CountAsync();
        var orderNumber = $"#VY{DateTime.UtcNow:yyMM}{(count + 1001):D4}";

        var order = new Order
        {
            OrderNumber = orderNumber, CustomerId = customerId,
            StoreId = req.StoreId, ServiceOfferingId = req.ServiceOfferingId,
            Status = OrderStatus.Pending, PaymentState = PaymentState.Unpaid,
            PaymentMethod = req.PaymentMethod,
            EstimatedWeight = req.EstimatedWeight, EstimatedPieces = req.EstimatedPieces,
            EstimatedLaundryCost = estimate.LaundryCost,
            PickupFee = estimate.PickupFee, DeliveryFee = estimate.DeliveryFee,
            TotalEstimate = estimate.TotalEstimate,
            PickupFeeAmount = estimate.PickupFeePayNow,
            BalanceAmount = estimate.BalanceDueOnDelivery,
            PickupAddress = req.PickupAddress, DeliveryAddress = req.DeliveryAddress,
            RequestedPickupDate = req.RequestedPickupDate,
            RequestedPickupSlot = req.RequestedPickupSlot,
            SpecialInstructions = req.SpecialInstructions
        };
        _db.Orders.Add(order);
        await _db.SaveChangesAsync();

        // Persist order items (multi-service support)
        if (req.Items is { Count: > 0 })
        {
            foreach (var item in req.Items)
            {
                var itemSvc = await _db.ServiceOfferings
                    .FirstOrDefaultAsync(s => s.Id == item.ServiceOfferingId && s.IsActive);
                if (itemSvc == null) continue;
                var lineTotal = itemSvc.PricingMode == PricingMode.PerKg
                    ? Math.Max(item.Weight * itemSvc.BasePrice, itemSvc.MinimumCharge)
                    : Math.Max(item.Pieces * itemSvc.BasePrice, itemSvc.MinimumCharge);
                _db.OrderItems.Add(new OrderItem
                {
                    OrderId = order.Id,
                    ServiceOfferingId = itemSvc.Id,
                    ServiceName = itemSvc.Name,
                    PricingMode = itemSvc.PricingMode.ToString(),
                    Weight = item.Weight,
                    Pieces = item.Pieces,
                    UnitPrice = itemSvc.BasePrice,
                    LineTotal = lineTotal
                });
            }
            await _db.SaveChangesAsync();
        }
        else
        {
            // Fallback: persist the primary service as a single item
            _db.OrderItems.Add(new OrderItem
            {
                OrderId = order.Id,
                ServiceOfferingId = svc.Id,
                ServiceName = svc.Name,
                PricingMode = svc.PricingMode.ToString(),
                Weight = req.EstimatedWeight,
                Pieces = req.EstimatedPieces,
                UnitPrice = svc.BasePrice,
                LineTotal = estimate.LaundryCost
            });
            await _db.SaveChangesAsync();
        }

        _db.OrderStatusHistories.Add(new OrderStatusHistory
        {
            OrderId = order.Id, Status = OrderStatus.Pending,
            Note = "Order placed by customer"
        });
        svc.Store.TotalOrders++;
        await _db.SaveChangesAsync();

        var customer = await _db.Users.FindAsync(customerId);
        if (customer != null)
        {
            await _notifications.SendSmsAsync(customer.Phone,
                $"Hi {customer.FullName.Split(' ')[0]}, your VYRON order {orderNumber} is confirmed! " +
                $"Pay ₦{estimate.PickupFeePayNow:N0} pickup fee to proceed.");
            await _notifications.SendInAppAsync(customerId,
                "Order Placed",
                $"Your order #{orderNumber} has been placed. Pay ₦{estimate.PickupFeePayNow:N0} pickup fee to get started.",
                "order");
        }

        await _audit.LogAsync(customerId, "ORDER_CREATE", "Order", order.Id);
        return (await GetOrderDtoAsync(order.Id))!;
    }

    public async Task<OrderDto?> GetOrderAsync(Guid id) => await GetOrderDtoAsync(id);

    public async Task<OrderDto?> GetOrderByNumberAsync(string number)
    {
        var o = await _db.Orders.FirstOrDefaultAsync(x => x.OrderNumber == number);
        return o == null ? null : await GetOrderDtoAsync(o.Id);
    }

    public async Task<List<OrderDto>> GetCustomerOrdersAsync(Guid customerId, int page = 1)
    {
        var ids = await _db.Orders.Where(o => o.CustomerId == customerId)
            .OrderByDescending(o => o.CreatedAt).Skip((page - 1) * 20).Take(20)
            .Select(o => o.Id).ToListAsync();
        return await LoadOrderDtosAsync(ids);
    }

    public async Task<List<OrderDto>> GetAllOrdersAsync(OrderStatus? status, string? search, int page = 1)
    {
        var q = _db.Orders.Include(o => o.Customer).Include(o => o.Store).AsQueryable();
        if (status.HasValue) q = q.Where(o => o.Status == status.Value);
        if (!string.IsNullOrEmpty(search))
            q = q.Where(o => o.OrderNumber.Contains(search) || o.Customer.FullName.Contains(search)
                          || o.Customer.Phone.Contains(search) || o.Store.Name.Contains(search));
        var ids = await q.OrderByDescending(o => o.CreatedAt).Skip((page - 1) * 25).Take(25)
            .Select(o => o.Id).ToListAsync();
        return await LoadOrderDtosAsync(ids);
    }

    public async Task<List<OrderDto>> GetStoreOrdersAsync(Guid storeId, int page = 1)
    {
        var ids = await _db.Orders.Where(o => o.StoreId == storeId)
            .OrderByDescending(o => o.CreatedAt).Skip((page - 1) * 25).Take(25)
            .Select(o => o.Id).ToListAsync();
        return await LoadOrderDtosAsync(ids);
    }

    public async Task<List<OrderDto>> GetRiderOrdersAsync(Guid riderId)
    {
        var ids = await _db.Orders.Where(o => o.RiderId == riderId
                && o.Status != OrderStatus.Completed && o.Status != OrderStatus.Cancelled)
            .OrderByDescending(o => o.CreatedAt).Select(o => o.Id).ToListAsync();
        return await LoadOrderDtosAsync(ids);
    }

    public async Task<OrderDto?> UpdateStatusAsync(Guid orderId, OrderStatus newStatus, string? note, Guid? changedBy)
    {
        var order = await _db.Orders.Include(o => o.Customer).Include(o => o.Store)
            .FirstOrDefaultAsync(o => o.Id == orderId);
        if (order == null) return null;

        var oldStatus = order.Status;
        order.Status = newStatus; order.UpdatedAt = DateTime.UtcNow;
        ApplyTimestamp(order, newStatus);

        if (newStatus == OrderStatus.Delivered) order.PaymentState = PaymentState.BalancePending;
        if (newStatus == OrderStatus.Completed) order.PaymentState = PaymentState.FullyPaid;

        _db.OrderStatusHistories.Add(new OrderStatusHistory
        {
            OrderId = orderId, Status = newStatus,
            Note = note ?? GetDefaultNote(newStatus), ChangedByUserId = changedBy
        });
        await _db.SaveChangesAsync();

        await _hub.Clients.Group($"order-{order.OrderNumber}")
            .SendAsync("OrderStatusUpdated", new
            {
                orderNumber = order.OrderNumber,
                status = (int)newStatus,
                statusName = newStatus.ToString(),
                updatedAt = DateTime.UtcNow
            });

        var sms = GetStatusSms(order.Customer.FullName.Split(' ')[0],
            order.OrderNumber, order.Store.Name, newStatus);
        if (sms != null) await _notifications.SendSmsAsync(order.Customer.Phone, sms);

        var inApp = GetStatusInAppMessage(order.OrderNumber, newStatus);
        if (inApp != null)
            await _notifications.SendInAppAsync(order.CustomerId,
                GetStatusInAppTitle(newStatus), inApp, "order");

        await _audit.LogAsync(changedBy, "ORDER_STATUS_UPDATE", "Order", orderId,
            oldStatus.ToString(), newStatus.ToString());
        return await GetOrderDtoAsync(orderId);
    }

    public async Task<OrderDto?> AssignRiderAsync(Guid orderId, Guid riderId, Guid adminId)
    {
        var order = await _db.Orders.FindAsync(orderId);
        var rider = await _db.Riders.Include(r => r.User).FirstOrDefaultAsync(r => r.Id == riderId);
        if (order == null || rider == null) return null;

        order.RiderId = riderId; order.RiderAssignedAt = DateTime.UtcNow;
        if (order.Status == OrderStatus.PickupFeePaid) order.Status = OrderStatus.RiderAssigned;
        order.UpdatedAt = DateTime.UtcNow;

        _db.OrderStatusHistories.Add(new OrderStatusHistory
        {
            OrderId = orderId, Status = OrderStatus.RiderAssigned,
            Note = $"Rider {rider.User.FullName} assigned", ChangedByUserId = adminId
        });
        await _db.SaveChangesAsync();
        // In-app notification for the customer whose order got a rider
        await _notifications.SendInAppAsync(order.CustomerId,
            "Rider Assigned",
            $"A rider has been assigned to your order #{order.OrderNumber}. They will pick up your laundry soon.",
            "order");
        await _audit.LogAsync(adminId, "RIDER_ASSIGN", "Order", orderId);
        return await GetOrderDtoAsync(orderId);
    }

    public async Task<OrderDto?> OverridePriceAsync(Guid orderId, OverridePriceRequest req, Guid adminId)
    {
        var order = await _db.Orders.FindAsync(orderId);
        if (order == null) return null;

        order.ActualLaundryCost = req.ActualLaundryCost;
        order.ActualTotal = req.ActualLaundryCost + order.PickupFee + order.DeliveryFee;
        order.BalanceAmount = req.ActualLaundryCost + order.DeliveryFee;
        order.AdminPriceOverride = true;
        order.AdminOverrideReason = req.Reason;
        order.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        await _audit.LogAsync(adminId, "PRICE_OVERRIDE", "Order", orderId,
            order.EstimatedLaundryCost.ToString(), req.ActualLaundryCost.ToString());
        return await GetOrderDtoAsync(orderId);
    }

    // ── Helpers ───────────────────────────────────────────────────
    private static void ApplyTimestamp(Order o, OrderStatus s)
    {
        var now = DateTime.UtcNow;
        switch (s)
        {
            case OrderStatus.PickupFeePaid: o.PickupFeePaidAt = now; break;
            case OrderStatus.PickedUp: o.PickedUpAt = now; break;
            case OrderStatus.Processing: o.ProcessingStartedAt = now; break;
            case OrderStatus.Ready: o.ReadyAt = now; break;
            case OrderStatus.OutForDelivery: o.OutForDeliveryAt = now; break;
            case OrderStatus.Delivered: o.DeliveredAt = now; break;
            case OrderStatus.Completed: o.CompletedAt = now; break;
        }
    }

    private static string GetDefaultNote(OrderStatus s) => s switch
    {
        OrderStatus.Confirmed => "Order confirmed by store",
        OrderStatus.PickupFeePaid => "Pickup fee received",
        OrderStatus.RiderAssigned => "Rider assigned for pickup",
        OrderStatus.PickedUp => "Laundry picked up",
        OrderStatus.Processing => "Laundry being processed",
        OrderStatus.Ready => "Laundry ready for delivery",
        OrderStatus.OutForDelivery => "Rider out for delivery",
        OrderStatus.Delivered => "Laundry delivered to customer",
        OrderStatus.Completed => "Order completed",
        _ => ""
    };

    private static string? GetStatusSms(string name, string order, string store, OrderStatus s) => s switch
    {
        OrderStatus.Confirmed => $"Hi {name}, your VYRON order {order} at {store} is confirmed! 🧺",
        OrderStatus.PickedUp => $"Hi {name}, your laundry ({order}) has been picked up.",
        OrderStatus.Ready => $"Hi {name}, your laundry ({order}) is clean and ready! ✨",
        OrderStatus.OutForDelivery => $"Hi {name}, your clean laundry ({order}) is on the way! 🚀",
        OrderStatus.Delivered => $"Hi {name}, order ({order}) delivered! Please pay balance. 👕",
        OrderStatus.Completed => $"Thank you for using VYRON! Order {order} complete. ⭐",
        _ => null
    };

    private static string GetStatusInAppTitle(OrderStatus s) => s switch
    {
        OrderStatus.Confirmed      => "Order Confirmed",
        OrderStatus.RiderAssigned  => "Rider Assigned",
        OrderStatus.PickedUp       => "Laundry Picked Up",
        OrderStatus.Processing     => "Laundry in Progress",
        OrderStatus.Ready          => "Ready for Delivery",
        OrderStatus.OutForDelivery => "On the Way!",
        OrderStatus.Delivered      => "Order Delivered",
        OrderStatus.Completed      => "Order Complete",
        _                          => "Order Update"
    };

    private static string? GetStatusInAppMessage(string orderNumber, OrderStatus s) => s switch
    {
        OrderStatus.Confirmed      => $"Your order #{orderNumber} has been confirmed by the store.",
        OrderStatus.PickedUp       => $"Your laundry for order #{orderNumber} has been picked up.",
        OrderStatus.Processing     => $"Your laundry (#{orderNumber}) is being cleaned and processed.",
        OrderStatus.Ready          => $"Your laundry (#{orderNumber}) is clean and ready for delivery!",
        OrderStatus.OutForDelivery => $"Your clean laundry (#{orderNumber}) is on its way to you!",
        OrderStatus.Delivered      => $"Order #{orderNumber} has been delivered. Please pay the balance to complete.",
        OrderStatus.Completed      => $"Order #{orderNumber} is complete. Thank you for using VYRON!",
        _ => null
    };

    private async Task<List<OrderDto>> LoadOrderDtosAsync(List<Guid> ids)
    {
        var list = new List<OrderDto>();
        foreach (var id in ids)
        {
            var dto = await GetOrderDtoAsync(id);
            if (dto != null) list.Add(dto);
        }
        return list;
    }

    private async Task<OrderDto?> GetOrderDtoAsync(Guid id)
    {
        var o = await _db.Orders
            .Include(x => x.Customer).Include(x => x.Store).Include(x => x.Service)
            .Include(x => x.Rider).ThenInclude(r => r!.User)
            .Include(x => x.StatusHistory)
            .Include(x => x.Items)
            .Include(x => x.Review).ThenInclude(r => r!.Customer)
            .Include(x => x.Dispute)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (o == null) return null;

        return new OrderDto(
            o.Id, o.OrderNumber,
            new CustomerSummaryDto(o.Customer.Id, o.Customer.FullName, o.Customer.Phone),
            new StoreSummaryDto(o.Store.Id, o.Store.Name, o.Store.Address, o.Store.AverageRating, o.Store.LogoUrl, o.Store.Phone),
            new ServiceSummaryDto(o.Service.Id, o.Service.ServiceType, o.Service.Name, o.Service.Description,
                o.Service.PricingMode, o.Service.BasePrice, o.Service.MinimumCharge,
                o.Service.IsActive, o.Service.EstimatedHours),
            o.Rider == null ? null : new RiderSummaryDto(o.Rider.Id, o.Rider.User.FullName,
                o.Rider.User.Phone, o.Rider.VehicleType, o.Rider.VehiclePlate),
            o.Status, o.Status.ToString(),
            o.PaymentState, o.PaymentState.ToString(),
            o.PaymentMethod,
            o.EstimatedWeight, o.EstimatedPieces,
            o.EstimatedLaundryCost, o.ActualLaundryCost,
            o.PickupFee, o.DeliveryFee, o.TotalEstimate, o.ActualTotal,
            o.PickupFeeAmount, o.BalanceAmount,
            o.AdminPriceOverride, o.AdminOverrideReason,
            o.PickupAddress, o.DeliveryAddress,
            o.RequestedPickupDate, o.RequestedPickupSlot, o.SpecialInstructions,
            o.PickedUpAt, o.ProcessingStartedAt, o.ReadyAt,
            o.OutForDeliveryAt, o.DeliveredAt, o.CompletedAt, o.CreatedAt,
            o.StatusHistory.OrderByDescending(h => h.ChangedAt)
                .Select(h => new StatusHistoryDto(h.Status, h.Status.ToString(), h.Note, h.ChangedAt))
                .ToList(),
            o.Review == null ? null : new ReviewDto(o.Review.Id, o.Review.OrderId, o.OrderNumber,
                o.Review.Customer.FullName, o.Review.Rating, o.Review.Comment,
                o.Review.PhotoUrl, o.Review.IsVisible, o.Review.CreatedAt),
            o.Dispute == null ? null : new DisputeSummaryDto(o.Dispute.Id, o.Dispute.Type,
                o.Dispute.Status, o.Dispute.CreatedAt),
            o.Items.Select(i => new OrderItemDto(
                i.Id, i.ServiceOfferingId, i.ServiceName,
                i.PricingMode, i.Weight, i.Pieces,
                i.UnitPrice, i.LineTotal)).ToList());
    }
}

// ═══════════════════════════════════════════════════════════════════
// DISPUTE SERVICE
// ═══════════════════════════════════════════════════════════════════
public interface IDisputeService
{
    Task<DisputeDetailDto> CreateDisputeAsync(Guid userId, CreateDisputeRequest req);
    Task<DisputeDetailDto?> GetDisputeAsync(Guid id);
    Task<List<DisputeDetailDto>> GetMyDisputesAsync(Guid userId);
    Task<List<DisputeDetailDto>> GetAllDisputesAsync(DisputeStatus? status = null, int page = 1);
    Task<DisputeDetailDto?> UpdateStatusAsync(Guid id, DisputeStatus status, string? note, Guid adminId);
    Task<DisputeDetailDto?> ResolveAsync(Guid id, ResolveDisputeRequest req, Guid adminId);
    Task<DisputeMessageDto> AddMessageAsync(Guid disputeId, Guid senderId, string message, bool isAdmin);
}

public class DisputeService : IDisputeService
{
    private readonly VyronDbContext _db;
    private readonly INotificationService _notifications;
    private readonly IAuditService _audit;

    public DisputeService(VyronDbContext db, INotificationService notifications, IAuditService audit)
    { _db = db; _notifications = notifications; _audit = audit; }

    public async Task<DisputeDetailDto> CreateDisputeAsync(Guid userId, CreateDisputeRequest req)
    {
        var order = await _db.Orders.Include(o => o.Customer).Include(o => o.Store)
            .FirstOrDefaultAsync(o => o.Id == req.OrderId)
            ?? throw new InvalidOperationException("Order not found.");

        if (await _db.Disputes.AnyAsync(d => d.OrderId == req.OrderId))
            throw new InvalidOperationException("A dispute already exists.");

        var dispute = new Dispute
        {
            OrderId = req.OrderId, RaisedByUserId = userId,
            Type = req.Type, Description = req.Description,
            EvidenceUrl = req.EvidenceUrl, Status = DisputeStatus.Open
        };
        _db.Disputes.Add(dispute);
        order.Status = OrderStatus.Disputed; order.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        await _audit.LogAsync(userId, "DISPUTE_RAISE", "Dispute", dispute.Id);
        return (await GetDisputeAsync(dispute.Id))!;
    }

    public Task<DisputeDetailDto?> GetDisputeAsync(Guid id) => LoadDisputeDto(id);

    public async Task<List<DisputeDetailDto>> GetMyDisputesAsync(Guid userId)
    {
        var ids = await _db.Disputes
            .Where(d => d.RaisedByUserId == userId)
            .OrderByDescending(d => d.CreatedAt)
            .Select(d => d.Id)
            .ToListAsync();
        var list = new List<DisputeDetailDto>();
        foreach (var id in ids) { var dto = await LoadDisputeDto(id); if (dto != null) list.Add(dto); }
        return list;
    }

    public async Task<List<DisputeDetailDto>> GetAllDisputesAsync(DisputeStatus? status, int page)
    {
        var q = _db.Disputes.AsQueryable();
        if (status.HasValue) q = q.Where(d => d.Status == status.Value);
        var ids = await q.OrderByDescending(d => d.CreatedAt).Skip((page - 1) * 25).Take(25)
            .Select(d => d.Id).ToListAsync();
        var list = new List<DisputeDetailDto>();
        foreach (var id in ids) { var dto = await LoadDisputeDto(id); if (dto != null) list.Add(dto); }
        return list;
    }

    public async Task<DisputeDetailDto?> UpdateStatusAsync(Guid id, DisputeStatus status, string? note, Guid adminId)
    {
        var d = await _db.Disputes.FindAsync(id);
        if (d == null) return null;
        d.Status = status; d.UpdatedAt = DateTime.UtcNow;
        if (note != null) d.AdminNotes = (d.AdminNotes ?? "") + $"\n[{DateTime.UtcNow:MMM dd HH:mm}] {note}";
        await _db.SaveChangesAsync();
        await _audit.LogAsync(adminId, "DISPUTE_STATUS", "Dispute", id);
        return await LoadDisputeDto(id);
    }

    public async Task<DisputeDetailDto?> ResolveAsync(Guid id, ResolveDisputeRequest req, Guid adminId)
    {
        var d = await _db.Disputes.Include(x => x.Order).ThenInclude(o => o.Customer)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (d == null) return null;

        d.Resolution = req.Resolution; d.ResolutionNote = req.ResolutionNote;
        d.RefundAmount = req.RefundAmount; d.ResolvedAt = DateTime.UtcNow;
        d.Status = req.Resolution is DisputeResolution.Refund or DisputeResolution.PartialRefund
            ? DisputeStatus.Refunded : DisputeStatus.Resolved;
        d.UpdatedAt = DateTime.UtcNow;

        if (d.Status == DisputeStatus.Refunded)
            d.Order.PaymentState = PaymentState.Refunded;

        await _db.SaveChangesAsync();
        await _notifications.SendSmsAsync(d.Order.Customer.Phone,
            $"Hi {d.Order.Customer.FullName.Split(' ')[0]}, your dispute is resolved. {req.ResolutionNote}");
        await _audit.LogAsync(adminId, "DISPUTE_RESOLVE", "Dispute", id);
        return await LoadDisputeDto(id);
    }

    public async Task<DisputeMessageDto> AddMessageAsync(Guid disputeId, Guid senderId, string message, bool isAdmin)
    {
        var msg = new DisputeMessage
        {
            DisputeId = disputeId, SenderId = senderId,
            Message = message, IsAdminMessage = isAdmin
        };
        _db.DisputeMessages.Add(msg);
        await _db.SaveChangesAsync();
        var sender = await _db.Users.FindAsync(senderId);
        return new DisputeMessageDto(msg.Id, sender?.FullName ?? "Unknown", isAdmin, message, msg.SentAt);
    }

    private async Task<DisputeDetailDto?> LoadDisputeDto(Guid id)
    {
        var d = await _db.Disputes.Include(x => x.RaisedBy).Include(x => x.Order)
            .Include(x => x.Messages).ThenInclude(m => m.Sender)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (d == null) return null;
        return new DisputeDetailDto(d.Id, d.OrderId, d.Order.OrderNumber,
            d.RaisedBy.FullName, d.RaisedBy.Phone,
            d.Type, d.Type.ToString(), d.Status, d.Status.ToString(),
            d.Description, d.EvidenceUrl, d.Resolution, d.ResolutionNote,
            d.RefundAmount, d.AdminNotes, d.CreatedAt, d.ResolvedAt,
            d.Messages.OrderBy(m => m.SentAt)
                .Select(m => new DisputeMessageDto(m.Id, m.Sender.FullName,
                    m.IsAdminMessage, m.Message, m.SentAt)).ToList());
    }
}

// ═══════════════════════════════════════════════════════════════════
// REVIEW SERVICE
// ═══════════════════════════════════════════════════════════════════
public interface IReviewService
{
    Task<ReviewDto> CreateReviewAsync(Guid customerId, CreateReviewRequest req);
    Task<List<ReviewDto>> GetStoreReviewsAsync(Guid storeId, int page = 1);
    Task<List<ReviewDto>> GetAllReviewsAsync(int page = 1);
    Task<bool> ModerateReviewAsync(Guid id, bool isVisible, string? adminNote, Guid adminId);
}

public class ReviewService : IReviewService
{
    private readonly VyronDbContext _db;
    private readonly IStoreService _stores;
    private readonly IAuditService _audit;

    public ReviewService(VyronDbContext db, IStoreService stores, IAuditService audit)
    { _db = db; _stores = stores; _audit = audit; }

    public async Task<ReviewDto> CreateReviewAsync(Guid customerId, CreateReviewRequest req)
    {
        var order = await _db.Orders.Include(o => o.Store)
            .FirstOrDefaultAsync(o => o.Id == req.OrderId && o.CustomerId == customerId
                && (o.Status == OrderStatus.Delivered || o.Status == OrderStatus.Completed
                    || o.Status == OrderStatus.BalancePaid))
            ?? throw new InvalidOperationException("Order not eligible for review. Order must be Delivered, BalancePaid, or Completed.");

        if (await _db.Reviews.AnyAsync(r => r.OrderId == req.OrderId))
            throw new InvalidOperationException("Already reviewed.");

        var review = new Review
        {
            OrderId = req.OrderId, CustomerId = customerId, StoreId = order.StoreId,
            Rating = Math.Clamp(req.Rating, 1, 5),
            Comment = req.Comment, PhotoUrl = req.PhotoUrl
        };
        _db.Reviews.Add(review);
        await _db.SaveChangesAsync();
        await _stores.RecalculateRatingAsync(order.StoreId);

        var customer = await _db.Users.FindAsync(customerId);
        return new ReviewDto(review.Id, review.OrderId, order.OrderNumber,
            customer?.FullName ?? "", review.Rating, review.Comment,
            review.PhotoUrl, review.IsVisible, review.CreatedAt);
    }

    public async Task<List<ReviewDto>> GetStoreReviewsAsync(Guid storeId, int page = 1) =>
        await _db.Reviews.Include(r => r.Customer).Include(r => r.Order)
            .Where(r => r.StoreId == storeId && r.IsVisible)
            .OrderByDescending(r => r.CreatedAt).Skip((page - 1) * 20).Take(20)
            .Select(r => new ReviewDto(r.Id, r.OrderId, r.Order.OrderNumber,
                r.Customer.FullName, r.Rating, r.Comment, r.PhotoUrl, r.IsVisible, r.CreatedAt))
            .ToListAsync();

    public async Task<List<ReviewDto>> GetAllReviewsAsync(int page = 1) =>
        await _db.Reviews.Include(r => r.Customer).Include(r => r.Order)
            .OrderByDescending(r => r.CreatedAt).Skip((page - 1) * 25).Take(25)
            .Select(r => new ReviewDto(r.Id, r.OrderId, r.Order.OrderNumber,
                r.Customer.FullName, r.Rating, r.Comment, r.PhotoUrl, r.IsVisible, r.CreatedAt))
            .ToListAsync();

    public async Task<bool> ModerateReviewAsync(Guid id, bool isVisible, string? adminNote, Guid adminId)
    {
        var review = await _db.Reviews.FindAsync(id);
        if (review == null) return false;
        review.IsVisible = isVisible; review.IsFlagged = !isVisible; review.AdminNote = adminNote;
        await _db.SaveChangesAsync();
        await _stores.RecalculateRatingAsync(review.StoreId);
        await _audit.LogAsync(adminId, "REVIEW_MODERATE", "Review", id);
        return true;
    }
}

// ═══════════════════════════════════════════════════════════════════
// PAYMENT SERVICE
// ═══════════════════════════════════════════════════════════════════
public interface IPaymentService
{
    Task<PaymentDto> RecordPaymentAsync(RecordPaymentRequest req, Guid userId);
    Task<List<PaymentDto>> GetOrderPaymentsAsync(Guid orderId);
}

public class PaymentService : IPaymentService
{
    private readonly VyronDbContext _db;
    private readonly IAuditService _audit;

    public PaymentService(VyronDbContext db, IAuditService audit)
    { _db = db; _audit = audit; }

    public async Task<PaymentDto> RecordPaymentAsync(RecordPaymentRequest req, Guid userId)
    {
        var order = await _db.Orders.FindAsync(req.OrderId)
            ?? throw new InvalidOperationException("Order not found.");

        // Guard: prevent duplicate pickup-fee payment
        if ((req.Type.Equals("PickupFee", StringComparison.OrdinalIgnoreCase) ||
             req.Type.Equals("pickup_fee", StringComparison.OrdinalIgnoreCase)) &&
            order.PaymentState >= PaymentState.PickupFeePaid)
            throw new InvalidOperationException("Pickup fee has already been paid for this order.");

        // Guard: prevent duplicate balance payment
        if (req.Type.Equals("Balance", StringComparison.OrdinalIgnoreCase) &&
            order.PaymentState == PaymentState.FullyPaid)
            throw new InvalidOperationException("Balance has already been paid for this order.");

        var payment = new Payment
        {
            OrderId = req.OrderId,
            PaymentRef = $"VY-PAY-{Guid.NewGuid().ToString()[..8].ToUpper()}",
            Amount = req.Amount, Method = req.Method, Type = req.Type,
            IsSuccessful = true, GatewayRef = req.GatewayRef, Notes = req.Notes
        };
        _db.Payments.Add(payment);

        // Accept both PascalCase ("PickupFee") and legacy snake_case ("pickup_fee")
        if (req.Type.Equals("PickupFee", StringComparison.OrdinalIgnoreCase) ||
            req.Type.Equals("pickup_fee", StringComparison.OrdinalIgnoreCase))
        {
            order.PaymentState = PaymentState.PickupFeePaid;
            order.PickupFeePaidAt = DateTime.UtcNow;
            if (order.Status == OrderStatus.Pending || order.Status == OrderStatus.Confirmed)
                order.Status = OrderStatus.PickupFeePaid;
        }
        else if (req.Type.Equals("Balance", StringComparison.OrdinalIgnoreCase) ||
                 req.Type.Equals("balance", StringComparison.OrdinalIgnoreCase))
        {
            order.PaymentState = PaymentState.FullyPaid;
            order.BalancePaidAt = DateTime.UtcNow;
            if (order.Status == OrderStatus.Delivered) order.Status = OrderStatus.BalancePaid;
        }
        order.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        await _audit.LogAsync(userId, "PAYMENT_RECORD", "Payment", payment.Id);

        return new PaymentDto(payment.Id, payment.PaymentRef, payment.Amount,
            payment.Method, payment.Type, payment.IsSuccessful, payment.GatewayRef, payment.CreatedAt);
    }

    public async Task<List<PaymentDto>> GetOrderPaymentsAsync(Guid orderId) =>
        await _db.Payments.Where(p => p.OrderId == orderId)
            .Select(p => new PaymentDto(p.Id, p.PaymentRef, p.Amount, p.Method,
                p.Type, p.IsSuccessful, p.GatewayRef, p.CreatedAt))
            .ToListAsync();
}

// ═══════════════════════════════════════════════════════════════════
// ANALYTICS
// ═══════════════════════════════════════════════════════════════════
public interface IAnalyticsService
{
    Task<DashboardSummaryDto> GetDashboardAsync();
}

public class AnalyticsService : IAnalyticsService
{
    private readonly VyronDbContext _db;
    public AnalyticsService(VyronDbContext db) => _db = db;

    public async Task<DashboardSummaryDto> GetDashboardAsync()
    {
        var now = DateTime.UtcNow;
        var today = now.Date;
        var weekStart = today.AddDays(-(int)today.DayOfWeek);
        var monthStart = new DateTime(now.Year, now.Month, 1);

        var all = await _db.Orders.ToListAsync();
        var todayOrders = all.Where(o => o.CreatedAt.Date == today).ToList();
        var weekOrders = all.Where(o => o.CreatedAt >= weekStart).ToList();
        var monthOrders = all.Where(o => o.CreatedAt >= monthStart).ToList();

        decimal Total(Order o) => o.ActualTotal > 0 ? o.ActualTotal : o.TotalEstimate;

        var revenueToday = todayOrders.Where(o => o.PaymentState == PaymentState.FullyPaid).Sum(Total);
        var revenueWeek = weekOrders.Where(o => o.PaymentState == PaymentState.FullyPaid).Sum(Total);
        var revenueMonth = monthOrders.Where(o => o.PaymentState == PaymentState.FullyPaid).Sum(Total);

        var daily = new List<DailyMetric>();
        for (int i = 0; i < 7; i++)
        {
            var d = weekStart.AddDays(i);
            var dayOrders = all.Where(o => o.CreatedAt.Date == d.Date).ToList();
            daily.Add(new DailyMetric(d.ToString("ddd"), dayOrders.Count,
                dayOrders.Where(o => o.PaymentState == PaymentState.FullyPaid).Sum(o => o.TotalEstimate)));
        }

        return new DashboardSummaryDto(
            todayOrders.Count,
            all.Count(o => o.Status == OrderStatus.RiderAssigned),
            all.Count(o => o.Status == OrderStatus.Processing),
            all.Count(o => o.Status == OrderStatus.OutForDelivery),
            await _db.Disputes.CountAsync(d => d.Status == DisputeStatus.Open || d.Status == DisputeStatus.UnderReview),
            revenueToday, revenueWeek, revenueMonth,
            await _db.Stores.CountAsync(s => s.Status == StoreStatus.Active),
            await _db.Riders.CountAsync(r => r.Status == RiderStatus.OnlineAvailable),
            await _db.Users.CountAsync(u => u.Role == UserRole.Customer),
            all.Count(o => o.Status == OrderStatus.Completed),
            daily,
            all.GroupBy(o => o.Status).Select(g => new StatusCount(g.Key.ToString(), g.Count())).ToList());
    }
}
