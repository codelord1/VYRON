using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;
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
    /// <summary>Store owner or admin manually opens/closes a store.</summary>
    Task<bool> SetStoreOpenAsync(Guid storeId, bool open, Guid userId);
}

public class StoreService : IStoreService
{
    private readonly VyronDbContext _db;
    private readonly IAuditService _audit;
    private readonly IHttpContextAccessor _httpCtx;

    public StoreService(VyronDbContext db, IAuditService audit, IHttpContextAccessor httpCtx)
    { _db = db; _audit = audit; _httpCtx = httpCtx; }

    /// <summary>
    /// Converts a stored relative URL (e.g. /uploads/stores/x.jpg) to an absolute URL
    /// using the current request's scheme and host so the Android phone can load the image.
    /// Absolute URLs (http/https) are returned as-is. Null/empty returns null.
    /// </summary>
    private string? ResolveImageUrl(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        if (path.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return path;
        var req = _httpCtx.HttpContext?.Request;
        if (req == null) return path; // fallback: return as-is if no context
        var baseUrl = $"{req.Scheme}://{req.Host}";
        return $"{baseUrl}{(path.StartsWith('/') ? path : '/' + path)}";
    }

    public async Task<List<StoreListItemDto>> GetStoresAsync(string? search, string? sort,
        string? filter, double? lat, double? lng)
    {
        var query = _db.Stores.Include(s => s.Services)
            .AsNoTracking()
            .Where(s => s.Status == StoreStatus.Active).AsQueryable();

        if (!string.IsNullOrEmpty(search))
            query = query.Where(s =>
                s.Name.Contains(search) ||
                s.Area.Contains(search) ||
                s.Address.Contains(search) ||
                s.Services.Any(svc => svc.IsActive && svc.Name.Contains(search)));

        if (filter == "verified") query = query.Where(s => s.IsVerified);
        else if (filter == "fast") query = query.Where(s => s.FastPickup);
        else if (filter == "toprated") query = query.Where(s => s.IsTopRated);

        // Push DB-safe sorts into the query before materialising; distance needs Haversine in memory
        IQueryable<LaundryStore> orderedQuery = sort switch
        {
            "rating"   => query.OrderByDescending(s => s.AverageRating),
            "price"    => query.OrderBy(s => s.PickupFee),
            "fast"     => query.OrderBy(s => s.EstimatedPickupMinutes),
            _          => query.OrderByDescending(s => s.IsVerified).ThenByDescending(s => s.AverageRating)
        };

        // Safety cap: never pull more than 200 active stores in one call
        var stores = await orderedQuery.Take(200).ToListAsync();

        // Distance sort happens in memory (Haversine can't translate to SQL)
        IEnumerable<LaundryStore> sorted = (sort == "distance" && lat.HasValue && lng.HasValue)
            ? stores.OrderBy(s => Haversine(lat.Value, lng.Value, s.Latitude, s.Longitude))
            : stores;

        return sorted.Select(s => MapToListItem(s, ResolveImageUrl(s.LogoUrl))).ToList();
    }

    public async Task<StoreDetailDto?> GetStoreAsync(Guid id)
    {
        // AsSplitQuery: Services and Reviews are both collection navigations on
        // LaundryStore. Loading both in one SingleQuery produces a cartesian product
        // (MultipleCollectionIncludeWarning). Split queries issue separate SELECTs
        // and are faster for a filtered, bounded collection like Reviews(Take(10)).
        var store = await _db.Stores
            .AsNoTracking()
            .AsSplitQuery()
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
            store.OpeningHours, ResolveImageUrl(store.LogoUrl), ResolveImageUrl(store.BannerUrl),
            store.Status, store.CreatedAt,
            store.Services.Where(s => s.IsActive).Select(MapService).ToList(),
            store.Reviews.Select(MapReview).ToList(),
            ComputeIsOpen(store));
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

    public async Task<bool> SetStoreOpenAsync(Guid storeId, bool open, Guid userId)
    {
        var store = await _db.Stores.FindAsync(storeId);
        if (store == null) return false;
        store.IsManuallyClosed = !open;
        if (open)  store.LastOpenedAt = DateTime.UtcNow;
        else       store.LastClosedAt = DateTime.UtcNow;
        store.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        await _audit.LogAsync(userId, open ? "STORE_OPEN" : "STORE_CLOSE", "LaundryStore", storeId);
        return true;
    }

    public async Task RecalculateRatingAsync(Guid storeId)
    {
        // Server-side aggregation — no full review load into memory
        var count = await _db.Reviews.CountAsync(r => r.StoreId == storeId && r.IsVisible);
        var avg   = count > 0
            ? await _db.Reviews.Where(r => r.StoreId == storeId && r.IsVisible)
                                .AverageAsync(r => (double)r.Rating)
            : 0.0;
        var store = await _db.Stores.FindAsync(storeId);
        if (store == null) return;
        store.TotalReviews  = count;
        store.AverageRating = Math.Round((decimal)avg, 1);
        await _db.SaveChangesAsync();
    }

    /// <summary>
    /// Computes real-time open status: respects IsManuallyClosed, structured hours, and opening days.
    /// Falls back to true (open) when no structured hours are configured.
    /// </summary>
    internal static bool ComputeIsOpenStatic(LaundryStore s) => ComputeIsOpen(s);

    private static bool ComputeIsOpen(LaundryStore s)
    {
        if (s.IsManuallyClosed) return false;

        if (s.OpeningTime.HasValue && s.ClosingTime.HasValue)
        {
            var now = TimeOnly.FromDateTime(DateTime.Now);

            if (!string.IsNullOrEmpty(s.OpeningDays))
            {
                var dayAbbr = DateTime.Today.DayOfWeek switch
                {
                    DayOfWeek.Monday    => "Mon", DayOfWeek.Tuesday   => "Tue",
                    DayOfWeek.Wednesday => "Wed", DayOfWeek.Thursday  => "Thu",
                    DayOfWeek.Friday    => "Fri", DayOfWeek.Saturday  => "Sat",
                    _                   => "Sun"
                };
                if (!s.OpeningDays.Contains(dayAbbr, StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            // Handle both normal and overnight spans
            return s.ClosingTime.Value > s.OpeningTime.Value
                ? now >= s.OpeningTime.Value && now <= s.ClosingTime.Value
                : now >= s.OpeningTime.Value || now <= s.ClosingTime.Value;
        }

        return true; // No structured hours → assume open
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

    private static StoreListItemDto MapToListItem(LaundryStore s, string? resolvedLogoUrl) => new(
        s.Id, s.Name, s.Description, s.Address, s.Area,
        s.AverageRating, s.TotalReviews, s.TotalOrders,
        s.PickupFee, s.DeliveryFee, s.EstimatedPickupMinutes,
        s.IsVerified, s.IsTopRated, s.FastPickup, s.Status, resolvedLogoUrl,
        s.Latitude, s.Longitude,
        s.Services.Where(x => x.IsActive).Select(MapService).ToList(),
        ComputeIsOpen(s));

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
    /// <summary>
    /// Returns a customer's orders.
    /// group: null=all  "active"=in-progress  "completed"=Delivered/BalancePaid/Completed  "cancelled"=Cancelled/Disputed
    /// </summary>
    Task<List<OrderDto>> GetCustomerOrdersAsync(Guid customerId, int page = 1, string? group = null);
    Task<List<OrderDto>> GetAllOrdersAsync(OrderStatus? status = null, string? search = null, int page = 1);
    Task<List<OrderDto>> GetStoreOrdersAsync(Guid storeId, int page = 1);
    Task<List<OrderDto>> GetRiderOrdersAsync(Guid riderId);
    Task<OrderDto?> UpdateStatusAsync(Guid orderId, OrderStatus newStatus, string? note, Guid? changedBy);
    Task<OrderDto?> AssignRiderAsync(Guid orderId, Guid riderId, Guid adminId);
    /// <summary>Assigns a delivery rider (separate from the pickup rider) when the order is Ready.</summary>
    Task<OrderDto?> AssignDeliveryRiderAsync(Guid orderId, Guid riderId, Guid adminId);
    Task<OrderDto?> OverridePriceAsync(Guid orderId, OverridePriceRequest req, Guid adminId);
    /// <summary>
    /// Builds a reorder draft from a previous order: returns a prefilled estimate
    /// so CustomerApp can confirm before creating the new order.
    /// </summary>
    Task<ReorderDraftDto?> GetReorderDraftAsync(Guid originalOrderId, Guid customerId);
}

public class OrderService : IOrderService
{
    private readonly VyronDbContext _db;
    private readonly INotificationService _notifications;
    private readonly IAuditService _audit;
    private readonly IHubContext<OrderTrackingHub> _hub;
    private readonly ILogger<OrderService> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHttpContextAccessor _httpCtx;

    public OrderService(VyronDbContext db, INotificationService notifications,
        IAuditService audit, IHubContext<OrderTrackingHub> hub,
        ILogger<OrderService> logger, IServiceScopeFactory scopeFactory,
        IHttpContextAccessor httpCtx)
    {
        _db = db; _notifications = notifications; _audit = audit;
        _hub = hub; _logger = logger; _scopeFactory = scopeFactory;
        _httpCtx = httpCtx;
    }

    /// <summary>
    /// Converts a stored relative path (e.g. /uploads/stores/x.jpg) to an absolute URL
    /// using the current request host so the mobile client can load the image directly.
    /// Absolute URLs pass through unchanged. Null/empty returns null.
    /// </summary>
    private string? ResolveImageUrl(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        if (path.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return path;
        var req = _httpCtx.HttpContext?.Request;
        if (req == null) return path;
        var baseUrl = $"{req.Scheme}://{req.Host}";
        return $"{baseUrl}{(path.StartsWith('/') ? path : '/' + path)}";
    }

    // ─── Order status group buckets ───────────────────────────────
    private static readonly HashSet<OrderStatus> ActiveStatuses = new()
    {
        OrderStatus.Pending, OrderStatus.Confirmed, OrderStatus.PickupFeePaid,
        OrderStatus.RiderAssigned, OrderStatus.PickUpRiderAssigned,
        OrderStatus.PickedUp, OrderStatus.Processing, OrderStatus.Ready,
        OrderStatus.OutForDelivery, OrderStatus.DeliveryRiderAssigned
    };
    private static readonly HashSet<OrderStatus> CompletedStatuses = new()
    {
        OrderStatus.Delivered, OrderStatus.BalancePaid, OrderStatus.Completed
    };
    private static readonly HashSet<OrderStatus> CancelledStatuses = new()
    {
        OrderStatus.Cancelled, OrderStatus.Disputed
    };

    /// <summary>
    /// Runs post-save side effects (notifications, audit) on a background thread with a
    /// fresh DI scope. The HTTP response is returned to the phone immediately.
    /// Exceptions are swallowed and logged — they must not bubble back to the caller.
    /// </summary>
    private void RunPostSave(Func<IServiceProvider, Task> work, string label)
    {
        _ = Task.Run(async () =>
        {
            using var scope = _scopeFactory.CreateScope();
            try   { await work(scope.ServiceProvider); }
            catch (Exception ex)
            { _logger.LogError(ex, "[PostSave] Background task '{Label}' failed", label); }
        });
    }

    public async Task<PriceEstimateResponse> EstimatePriceAsync(PriceEstimateRequest req)
    {
        var svc = await _db.ServiceOfferings.AsNoTracking().Include(s => s.Store)
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

        // Guard: reject orders when store is closed
        if (!StoreService.ComputeIsOpenStatic(svc.Store))
            throw new InvalidOperationException("This store is currently closed. Please try again later.");

        var estimate = await EstimatePriceAsync(new PriceEstimateRequest(req.ServiceOfferingId, req.EstimatedWeight, req.EstimatedPieces));
        // Use timestamp + random suffix instead of full-table CountAsync to avoid table scan
        var orderNumber = $"#VY{DateTime.UtcNow:yyMMddHHmm}{Random.Shared.Next(10, 99)}";

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
            // Pre-load all referenced service offerings in ONE query — avoids N+1
            var itemSvcIds = req.Items.Select(i => i.ServiceOfferingId).Distinct().ToList();
            var itemSvcMap = await _db.ServiceOfferings
                .Where(s => itemSvcIds.Contains(s.Id) && s.IsActive)
                .ToDictionaryAsync(s => s.Id);

            foreach (var item in req.Items)
            {
                if (!itemSvcMap.TryGetValue(item.ServiceOfferingId, out var itemSvc)) continue;
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

        // ── Fire-and-forget post-save side effects ────────────────
        // The order is already committed. Customer gets the response immediately.
        // Notifications and audit run on a background thread with a fresh DI scope.
        var capturedOrderId     = order.Id;
        var capturedOrderNumber = orderNumber;
        var capturedStoreId     = order.StoreId;
        var capturedPickupFee   = estimate.PickupFeePayNow;
        var capturedCustomerId  = customerId;

        RunPostSave(async sp =>
        {
            var db    = sp.GetRequiredService<VyronDbContext>();
            var notif = sp.GetRequiredService<INotificationService>();
            var audit = sp.GetRequiredService<IAuditService>();

            var customer = await db.Users.FindAsync(capturedCustomerId);
            if (customer != null)
            {
                await notif.SendSmsAsync(customer.Phone,
                    $"Hi {customer.FullName.Split(' ')[0]}, your VYRON order {capturedOrderNumber} is confirmed! " +
                    $"Pay ₦{capturedPickupFee:N0} pickup fee to proceed.");
                await notif.SendInAppAsync(capturedCustomerId,
                    "Order Placed",
                    $"Your order #{capturedOrderNumber} has been placed. Pay ₦{capturedPickupFee:N0} pickup fee to get started.",
                    "order", "Order", capturedOrderId);
            }
            await NotifyStoreUsersInternalAsync(db, notif, capturedStoreId,
                "New Order Received",
                $"Order #{capturedOrderNumber} has been placed by a customer. Please review and confirm.",
                "order", capturedOrderId, _logger);
            await audit.LogAsync(capturedCustomerId, "ORDER_CREATE", "Order", capturedOrderId);
        }, "CreateOrder");

        return (await GetOrderDtoAsync(order.Id))!;
    }

    public async Task<OrderDto?> GetOrderAsync(Guid id) => await GetOrderDtoAsync(id);

    public async Task<OrderDto?> GetOrderByNumberAsync(string number)
    {
        // Single query — no double round-trip
        var o = await OrdersWithIncludes().FirstOrDefaultAsync(x => x.OrderNumber == number);
        return o == null ? null : MapOrderToDto(o);
    }

    public async Task<List<OrderDto>> GetCustomerOrdersAsync(Guid customerId, int page = 1, string? group = null)
    {
        // Lean projection — single SQL query with scalar JOINs only.
        // No collection navigations (StatusHistory, Items, Review, Dispute) loaded here;
        // they are not needed for the orders list screen and caused the
        // MultipleCollectionIncludeWarning + cartesian explosion via LoadOrderDtosAsync.
        // Full data is available via GET /api/orders/{id}.
        var baseQuery = _db.Orders
            .AsNoTracking()
            .Where(o => o.CustomerId == customerId);

        // ── Status group filter (Task E) ─────────────────────────
        baseQuery = group?.ToLowerInvariant() switch
        {
            "active"    => baseQuery.Where(o => ActiveStatuses.Contains(o.Status)),
            "completed" => baseQuery.Where(o => CompletedStatuses.Contains(o.Status)),
            "cancelled" => baseQuery.Where(o => CancelledStatuses.Contains(o.Status)),
            _           => baseQuery  // null / unrecognised value → return all
        };

        var rows = await baseQuery
            .OrderByDescending(o => o.CreatedAt)
            .Skip((page - 1) * 20).Take(20)
            .Select(o => new {
                o.Id, o.OrderNumber,
                CustId    = o.CustomerId,
                CustName  = o.Customer.FullName,
                CustPhone = o.Customer.Phone,
                StoreId   = o.StoreId,
                StoreName = o.Store.Name, StoreAddr = o.Store.Address,
                StoreRating = o.Store.AverageRating, StoreLogo = o.Store.LogoUrl,
                StorePhone  = o.Store.Phone,
                SvcId      = o.ServiceOfferingId, SvcType = o.Service.ServiceType,
                SvcName    = o.Service.Name,      SvcDesc = o.Service.Description,
                SvcPricing = o.Service.PricingMode,
                SvcBase    = o.Service.BasePrice, SvcMin  = o.Service.MinimumCharge,
                SvcActive  = o.Service.IsActive,  SvcHours = o.Service.EstimatedHours,
                o.Status, o.PaymentState, o.PaymentMethod,
                o.EstimatedWeight, o.EstimatedPieces,
                o.EstimatedLaundryCost, o.ActualLaundryCost,
                o.PickupFee, o.DeliveryFee, o.TotalEstimate, o.ActualTotal,
                o.PickupFeeAmount, o.BalanceAmount,
                o.AdminPriceOverride, o.AdminOverrideReason,
                o.PickupAddress, o.DeliveryAddress,
                o.RequestedPickupDate, o.RequestedPickupSlot, o.SpecialInstructions,
                o.PickedUpAt, o.ProcessingStartedAt, o.ReadyAt,
                o.OutForDeliveryAt, o.DeliveredAt, o.CompletedAt, o.CreatedAt
            })
            .ToListAsync();

        // Map in memory — ToString() on enums cannot translate to SQL;
        // also resolve relative image paths to absolute URLs here (needs HttpContext).
        return rows.Select(o => new OrderDto(
            o.Id, o.OrderNumber,
            new CustomerSummaryDto(o.CustId, o.CustName, o.CustPhone),
            new StoreSummaryDto(o.StoreId, o.StoreName, o.StoreAddr,
                o.StoreRating, ResolveImageUrl(o.StoreLogo), o.StorePhone,
                ResolveImageUrl(o.StoreLogo)),   // PrimaryImageUrl falls back to LogoUrl
            new ServiceSummaryDto(o.SvcId, o.SvcType, o.SvcName, o.SvcDesc,
                o.SvcPricing, o.SvcBase, o.SvcMin, o.SvcActive, o.SvcHours),
            null,  // Rider — not needed on list screen
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
            new List<StatusHistoryDto>(), // empty — history available on detail endpoint
            null, null, null,
            new List<OrderItemDto>()      // empty — items available on detail endpoint
        )).ToList();
    }

    public async Task<List<OrderDto>> GetAllOrdersAsync(OrderStatus? status, string? search, int page = 1)
    {
        var q = _db.Orders.AsNoTracking()
            .Include(o => o.Customer).Include(o => o.Store).AsQueryable();
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
        var ids = await _db.Orders.AsNoTracking()
            .Where(o => o.StoreId == storeId)
            .OrderByDescending(o => o.CreatedAt).Skip((page - 1) * 25).Take(25)
            .Select(o => o.Id).ToListAsync();
        return await LoadOrderDtosAsync(ids);
    }

    public async Task<List<OrderDto>> GetRiderOrdersAsync(Guid riderId)
    {
        var ids = await _db.Orders.AsNoTracking()
            .Where(o => o.RiderId == riderId
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

        // SignalR push is instant and in-process — await it directly (no network call)
        await _hub.Clients.Group($"order-{order.OrderNumber}")
            .SendAsync("OrderStatusUpdated", new
            {
                orderNumber = order.OrderNumber,
                status = (int)newStatus,
                statusName = newStatus.ToString(),
                updatedAt = DateTime.UtcNow
            });

        // ── Fire-and-forget: SMS + in-app + store notify + audit ──
        var capturedOrderId     = orderId;
        var capturedOrderNumber = order.OrderNumber;
        var capturedCustomerId  = order.CustomerId;
        var capturedStoreId     = order.StoreId;
        var capturedPhone       = order.Customer.Phone;
        var capturedName        = order.Customer.FullName;
        var capturedStoreName   = order.Store.Name;
        var capturedOldStatus   = oldStatus;
        var capturedNewStatus   = newStatus;
        var capturedChangedBy   = changedBy;

        RunPostSave(async sp =>
        {
            var notif = sp.GetRequiredService<INotificationService>();
            var audit = sp.GetRequiredService<IAuditService>();
            var db    = sp.GetRequiredService<VyronDbContext>();

            var sms = GetStatusSms(capturedName.Split(' ')[0],
                capturedOrderNumber, capturedStoreName, capturedNewStatus);
            if (sms != null) await notif.SendSmsAsync(capturedPhone, sms);

            var inApp = GetStatusInAppMessage(capturedOrderNumber, capturedNewStatus);
            if (inApp != null)
                await notif.SendInAppAsync(capturedCustomerId,
                    GetStatusInAppTitle(capturedNewStatus), inApp, "order", "Order", capturedOrderId);

            var storeMsg = GetStoreStatusMessage(capturedOrderNumber, capturedNewStatus);
            if (storeMsg != null)
                await NotifyStoreUsersInternalAsync(db, notif, capturedStoreId,
                    GetStatusInAppTitle(capturedNewStatus), storeMsg, "order", capturedOrderId, _logger);

            await audit.LogAsync(capturedChangedBy, "ORDER_STATUS_UPDATE", "Order", capturedOrderId,
                capturedOldStatus.ToString(), capturedNewStatus.ToString());
        }, "UpdateStatus");

        return await GetOrderDtoAsync(orderId);
    }

    public async Task<OrderDto?> AssignRiderAsync(Guid orderId, Guid riderId, Guid adminId)
    {
        var order = await _db.Orders.FindAsync(orderId);
        var rider = await _db.Riders.Include(r => r.User).FirstOrDefaultAsync(r => r.Id == riderId);
        if (order == null || rider == null) return null;

        order.RiderId = riderId; order.RiderAssignedAt = DateTime.UtcNow;
        if (order.Status == OrderStatus.PickupFeePaid || order.Status == OrderStatus.RiderAssigned)
            order.Status = OrderStatus.PickUpRiderAssigned;
        order.UpdatedAt = DateTime.UtcNow;

        _db.OrderStatusHistories.Add(new OrderStatusHistory
        {
            OrderId = orderId, Status = OrderStatus.PickUpRiderAssigned,
            Note = $"Pickup rider {rider.User.FullName} assigned", ChangedByUserId = adminId
        });
        await _db.SaveChangesAsync();

        var capCustId   = order.CustomerId;
        var capOrderNum = order.OrderNumber;
        var capOrderId  = orderId;
        RunPostSave(async sp =>
        {
            var notif = sp.GetRequiredService<INotificationService>();
            var audit = sp.GetRequiredService<IAuditService>();
            await notif.SendInAppAsync(capCustId,
                "Pickup Rider Assigned",
                $"A pickup rider has been assigned to your order #{capOrderNum}. They will collect your laundry soon.",
                "order", "Order", capOrderId);
            await audit.LogAsync(adminId, "PICKUP_RIDER_ASSIGN", "Order", capOrderId);
        }, "AssignRider");

        return await GetOrderDtoAsync(orderId);
    }

    public async Task<OrderDto?> AssignDeliveryRiderAsync(Guid orderId, Guid riderId, Guid adminId)
    {
        var order = await _db.Orders.Include(o => o.Customer).FirstOrDefaultAsync(o => o.Id == orderId);
        var rider = await _db.Riders.Include(r => r.User).FirstOrDefaultAsync(r => r.Id == riderId);
        if (order == null || rider == null) return null;

        order.DeliveryRiderId = riderId; order.UpdatedAt = DateTime.UtcNow;

        // Set DeliveryRiderAssigned first; admin then promotes to OutForDelivery separately
        if (order.Status == OrderStatus.Ready || order.Status == OrderStatus.DeliveryRiderAssigned)
        {
            order.Status = OrderStatus.DeliveryRiderAssigned;
            _db.OrderStatusHistories.Add(new OrderStatusHistory
            {
                OrderId = orderId, Status = OrderStatus.DeliveryRiderAssigned,
                Note = $"Delivery rider {rider.User.FullName} assigned",
                ChangedByUserId = adminId
            });
        }

        await _db.SaveChangesAsync();

        var capCustId2   = order.CustomerId;
        var capOrderNum2 = order.OrderNumber;
        var capRiderName = rider.User.FullName;
        var capOrderId2  = orderId;
        RunPostSave(async sp =>
        {
            var notif = sp.GetRequiredService<INotificationService>();
            var audit = sp.GetRequiredService<IAuditService>();
            await notif.SendInAppAsync(capCustId2,
                "Delivery Rider Assigned",
                $"A delivery rider ({capRiderName}) has been assigned for your order #{capOrderNum2}. Your laundry will be on its way soon!",
                "order", "Order", capOrderId2);
            await audit.LogAsync(adminId, "DELIVERY_RIDER_ASSIGN", "Order", capOrderId2);
        }, "AssignDeliveryRider");

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
        OrderStatus.PickUpRiderAssigned => "Pickup rider assigned",
        OrderStatus.DeliveryRiderAssigned => "Delivery rider assigned",
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
        OrderStatus.Confirmed           => "Order Confirmed",
        OrderStatus.RiderAssigned       => "Rider Assigned",
        OrderStatus.PickUpRiderAssigned => "Pickup Rider Assigned",
        OrderStatus.DeliveryRiderAssigned => "Delivery Rider Assigned",
        OrderStatus.PickedUp            => "Laundry Picked Up",
        OrderStatus.Processing          => "Laundry in Progress",
        OrderStatus.Ready               => "Ready for Delivery",
        OrderStatus.OutForDelivery      => "On the Way!",
        OrderStatus.Delivered           => "Order Delivered",
        OrderStatus.Completed           => "Order Complete",
        _                               => "Order Update"
    };

    /// <summary>Messages sent to store staff when order status changes. Returns null if store doesn't need notifying.</summary>
    private static string? GetStoreStatusMessage(string orderNumber, OrderStatus s) => s switch
    {
        OrderStatus.PickupFeePaid  => $"Order #{orderNumber}: pickup fee has been paid. Please assign a rider for pickup.",
        OrderStatus.PickedUp       => $"Order #{orderNumber} has been picked up by the rider and is on its way.",
        OrderStatus.Delivered      => $"Order #{orderNumber} has been delivered. Balance payment is pending.",
        OrderStatus.Completed      => $"Order #{orderNumber} is complete. Payment received.",
        OrderStatus.Cancelled      => $"Order #{orderNumber} has been cancelled.",
        _ => null
    };

    private static string? GetStatusInAppMessage(string orderNumber, OrderStatus s) => s switch
    {
        OrderStatus.Confirmed             => $"Your order #{orderNumber} has been confirmed by the store.",
        OrderStatus.PickUpRiderAssigned   => $"A pickup rider has been assigned to order #{orderNumber}. They will collect your laundry soon.",
        OrderStatus.DeliveryRiderAssigned => $"A delivery rider has been assigned for order #{orderNumber}. Your laundry will be dispatched soon.",
        OrderStatus.PickedUp              => $"Your laundry for order #{orderNumber} has been picked up.",
        OrderStatus.Processing            => $"Your laundry (#{orderNumber}) is being cleaned and processed.",
        OrderStatus.Ready                 => $"Your laundry (#{orderNumber}) is clean and ready for delivery!",
        OrderStatus.OutForDelivery        => $"Your clean laundry (#{orderNumber}) is on its way to you!",
        OrderStatus.Delivered             => $"Order #{orderNumber} has been delivered. Please pay the balance to complete.",
        OrderStatus.Completed             => $"Order #{orderNumber} is complete. Thank you for using VYRON!",
        _ => null
    };

    /// <summary>
    /// Sends in-app notifications to the store owner + active assigned staff.
    /// Static so it can be safely called from fire-and-forget background scopes
    /// without capturing the request-scoped _db or _notifications fields.
    /// </summary>
    private static async Task NotifyStoreUsersInternalAsync(
        VyronDbContext db, INotificationService notif,
        Guid storeId, string title, string message,
        string entityType, Guid? entityId, ILogger logger)
    {
        try
        {
            var ownerId = await db.Stores.AsNoTracking()
                .Where(s => s.Id == storeId)
                .Select(s => (Guid?)s.OwnerId)
                .FirstOrDefaultAsync();
            if (ownerId.HasValue)
                await notif.SendInAppAsync(ownerId.Value, title, message, entityType, entityType, entityId);

            var staffUserIds = await db.StoreUserAssignments
                .Where(a => a.StoreId == storeId && a.IsActive)
                .Select(a => a.UserId)
                .ToListAsync();

            foreach (var uid in staffUserIds)
                await notif.SendInAppAsync(uid, title, message, entityType, entityType, entityId);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "NotifyStoreUsersInternalAsync failed for store {StoreId}", storeId);
        }
    }

    // ── Shared include chain — used only by DETAIL read paths ──────
    // AsSplitQuery: intentional — StatusHistory, Items, and StoreImages are all
    // collection navigations. Loading them in a SingleQuery produces a cartesian
    // explosion (MultipleCollectionIncludeWarning). Split queries issue separate
    // SELECTs per collection and re-join in memory — correct and faster.
    private IQueryable<Order> OrdersWithIncludes() =>
        _db.Orders
            .AsNoTracking()
            .AsSplitQuery()
            .Include(x => x.Customer)
            .Include(x => x.Store)
                .ThenInclude(s => s.StoreImages.Where(img => img.IsPrimary).Take(1))
            .Include(x => x.Service)
            .Include(x => x.Rider).ThenInclude(r => r!.User)
            .Include(x => x.DeliveryRider).ThenInclude(r => r!.User)
            .Include(x => x.StatusHistory)
            .Include(x => x.Items)
            .Include(x => x.Review).ThenInclude(r => r!.Customer)
            .Include(x => x.Dispute);

    /// <summary>
    /// Loads a page of order IDs in ONE batched query instead of N separate round-trips.
    /// Preserves the original ordering provided by the caller's ID list.
    /// </summary>
    private async Task<List<OrderDto>> LoadOrderDtosAsync(List<Guid> ids)
    {
        if (ids.Count == 0) return new();
        var orders = await OrdersWithIncludes()
            .Where(x => ids.Contains(x.Id))
            .ToListAsync();
        // Restore caller-specified order (DB may return rows in arbitrary order)
        return ids
            .Select(id => orders.FirstOrDefault(o => o.Id == id))
            .Where(o => o != null)
            .Select(o => MapOrderToDto(o!))
            .ToList();
    }

    private async Task<OrderDto?> GetOrderDtoAsync(Guid id)
    {
        var o = await OrdersWithIncludes().FirstOrDefaultAsync(x => x.Id == id);
        return o == null ? null : MapOrderToDto(o);
    }

    /// <summary>
    /// Maps a fully-loaded Order entity to its DTO.
    /// Instance method (not static) so it can call ResolveImageUrl for absolute URLs.
    /// </summary>
    private OrderDto MapOrderToDto(Order o)
    {
        // Primary store image: use the first IsPrimary StoreImage if loaded,
        // otherwise fall back to the store's LogoUrl.
        var primaryStoreImg = o.Store.StoreImages
            .FirstOrDefault(img => img.IsPrimary)?.ImagePath
            ?? o.Store.LogoUrl;

        return new OrderDto(
            o.Id, o.OrderNumber,
            new CustomerSummaryDto(o.Customer.Id, o.Customer.FullName, o.Customer.Phone),
            new StoreSummaryDto(o.Store.Id, o.Store.Name, o.Store.Address,
                o.Store.AverageRating, ResolveImageUrl(o.Store.LogoUrl), o.Store.Phone,
                ResolveImageUrl(primaryStoreImg)),
            new ServiceSummaryDto(o.Service.Id, o.Service.ServiceType, o.Service.Name,
                o.Service.Description, o.Service.PricingMode, o.Service.BasePrice,
                o.Service.MinimumCharge, o.Service.IsActive, o.Service.EstimatedHours),
            o.Rider == null ? null : new RiderSummaryDto(
                o.Rider.Id, o.Rider.User.FullName, o.Rider.User.Phone,
                o.Rider.VehicleType, o.Rider.VehiclePlate,
                ResolveImageUrl(o.Rider.User.ProfilePhoto)),   // AvatarUrl from User.ProfilePhoto
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
            o.DeliveryRider == null ? null : new RiderSummaryDto(
                o.DeliveryRider.Id, o.DeliveryRider.User.FullName, o.DeliveryRider.User.Phone,
                o.DeliveryRider.VehicleType, o.DeliveryRider.VehiclePlate,
                ResolveImageUrl(o.DeliveryRider.User.ProfilePhoto)), // DeliveryRider AvatarUrl
            o.Items.Select(i => new OrderItemDto(
                i.Id, i.ServiceOfferingId, i.ServiceName,
                i.PricingMode, i.Weight, i.Pieces,
                i.UnitPrice, i.LineTotal)).ToList());
    }

    // ── Reorder ───────────────────────────────────────────────────
    public async Task<ReorderDraftDto?> GetReorderDraftAsync(Guid originalOrderId, Guid customerId)
    {
        var original = await _db.Orders
            .AsNoTracking()
            .Include(o => o.Store)
            .Include(o => o.Service)
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == originalOrderId && o.CustomerId == customerId);
        if (original == null) return null;

        // Verify store is still active
        var storeActive = original.Store.Status == StoreStatus.Active;
        // Verify service is still active
        var serviceActive = original.Service.IsActive;

        // Build fresh estimate with current pricing
        decimal laundry = original.Service.PricingMode == PricingMode.PerKg
            ? Math.Max(original.EstimatedWeight * original.Service.BasePrice, original.Service.MinimumCharge)
            : Math.Max(original.EstimatedPieces * original.Service.BasePrice, original.Service.MinimumCharge);
        var total = laundry + original.Store.PickupFee + original.Store.DeliveryFee;

        return new ReorderDraftDto(
            OriginalOrderId: original.Id,
            OriginalOrderNumber: original.OrderNumber,
            StoreId: original.StoreId,
            StoreName: original.Store.Name,
            StoreActive: storeActive,
            ServiceOfferingId: original.ServiceOfferingId,
            ServiceName: original.Service.Name,
            ServiceActive: serviceActive,
            EstimatedWeight: original.EstimatedWeight,
            EstimatedPieces: original.EstimatedPieces,
            PickupAddress: original.PickupAddress,
            DeliveryAddress: original.DeliveryAddress,
            LaundryCost: laundry,
            PickupFee: original.Store.PickupFee,
            DeliveryFee: original.Store.DeliveryFee,
            TotalEstimate: total,
            PaymentMethod: original.PaymentMethod,
            CanReorder: storeActive && serviceActive);
    }
}

// ═══════════════════════════════════════════════════════════════════
// COUPON SERVICE
// ═══════════════════════════════════════════════════════════════════
public interface ICouponService
{
    /// <summary>Validates a coupon code for a specific customer and returns the discount info.</summary>
    Task<CouponValidationResult> ValidateAsync(string code, Guid customerId);
    /// <summary>Records a coupon usage against an order. Call after the order is committed.</summary>
    Task<bool> RecordUsageAsync(Guid couponId, Guid customerId, Guid orderId, decimal discountApplied);
    Task<List<Coupon>> GetAllAsync();
    Task<Coupon?> GetByIdAsync(Guid id);
    Task<Coupon> CreateAsync(Coupon coupon, Guid adminId);
    Task<bool> UpdateAsync(Coupon coupon, Guid adminId);
    Task<bool> ToggleActiveAsync(Guid id, Guid adminId);
}

public class CouponService : ICouponService
{
    private readonly VyronDbContext _db;
    private readonly IAuditService _audit;

    public CouponService(VyronDbContext db, IAuditService audit)
    { _db = db; _audit = audit; }

    public async Task<CouponValidationResult> ValidateAsync(string code, Guid customerId)
    {
        var now = DateTime.UtcNow;
        var coupon = await _db.Coupons.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Code == code.ToUpperInvariant());

        if (coupon == null)
            return new CouponValidationResult(false, "Coupon code not found.", null, null, 0);
        if (!coupon.IsActive)
            return new CouponValidationResult(false, "This coupon is no longer active.", null, null, 0);
        if (coupon.StartDate.HasValue && now < coupon.StartDate.Value)
            return new CouponValidationResult(false, "This coupon is not yet valid.", null, null, 0);
        if (coupon.EndDate.HasValue && now > coupon.EndDate.Value)
            return new CouponValidationResult(false, "This coupon has expired.", null, null, 0);
        if (coupon.GlobalMaxUses > 0 && coupon.TotalUses >= coupon.GlobalMaxUses)
            return new CouponValidationResult(false, "This coupon has reached its usage limit.", null, null, 0);

        // Per-customer use count
        var customerUses = await _db.CouponUsages.AsNoTracking()
            .CountAsync(u => u.CouponId == coupon.Id && u.CustomerId == customerId);
        if (coupon.MaxUsesPerCustomer > 0 && customerUses >= coupon.MaxUsesPerCustomer)
            return new CouponValidationResult(false, "You have already used this coupon the maximum number of times.", null, null, 0);

        // First-N-orders rule
        if (coupon.AppliesToFirstNOrders > 0)
        {
            var completedOrders = await _db.Orders.AsNoTracking()
                .CountAsync(o => o.CustomerId == customerId &&
                    o.Status != OrderStatus.Cancelled && o.Status != OrderStatus.Disputed);
            if (completedOrders >= coupon.AppliesToFirstNOrders)
                return new CouponValidationResult(false,
                    $"This coupon only applies to your first {coupon.AppliesToFirstNOrders} orders.", null, null, 0);
        }

        return new CouponValidationResult(true, "Coupon is valid!", coupon.Id, coupon.DiscountType, coupon.DiscountValue);
    }

    public async Task<bool> RecordUsageAsync(Guid couponId, Guid customerId, Guid orderId, decimal discountApplied)
    {
        var coupon = await _db.Coupons.FindAsync(couponId);
        if (coupon == null) return false;
        _db.CouponUsages.Add(new CouponUsage
        {
            CouponId = couponId, CustomerId = customerId,
            OrderId = orderId, DiscountApplied = discountApplied
        });
        coupon.TotalUses++;
        await _db.SaveChangesAsync();
        return true;
    }

    public Task<List<Coupon>> GetAllAsync() =>
        _db.Coupons.AsNoTracking().OrderByDescending(c => c.CreatedAt).ToListAsync();

    public Task<Coupon?> GetByIdAsync(Guid id) =>
        _db.Coupons.FirstOrDefaultAsync(c => c.Id == id);

    public async Task<Coupon> CreateAsync(Coupon coupon, Guid adminId)
    {
        coupon.Code = coupon.Code.ToUpperInvariant().Trim();
        coupon.CreatedByUserId = adminId;
        coupon.CreatedAt = coupon.UpdatedAt = DateTime.UtcNow;
        _db.Coupons.Add(coupon);
        await _db.SaveChangesAsync();
        await _audit.LogAsync(adminId, "COUPON_CREATE", "Coupon", coupon.Id);
        return coupon;
    }

    public async Task<bool> UpdateAsync(Coupon coupon, Guid adminId)
    {
        var c = await _db.Coupons.FindAsync(coupon.Id);
        if (c == null) return false;
        c.Code = coupon.Code.ToUpperInvariant().Trim();
        c.Description = coupon.Description;
        c.DiscountType = coupon.DiscountType;
        c.DiscountValue = coupon.DiscountValue;
        c.MaxUsesPerCustomer = coupon.MaxUsesPerCustomer;
        c.GlobalMaxUses = coupon.GlobalMaxUses;
        c.StartDate = coupon.StartDate;
        c.EndDate = coupon.EndDate;
        c.IsActive = coupon.IsActive;
        c.AppliesToFirstNOrders = coupon.AppliesToFirstNOrders;
        c.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        await _audit.LogAsync(adminId, "COUPON_UPDATE", "Coupon", coupon.Id);
        return true;
    }

    public async Task<bool> ToggleActiveAsync(Guid id, Guid adminId)
    {
        var c = await _db.Coupons.FindAsync(id);
        if (c == null) return false;
        c.IsActive = !c.IsActive;
        c.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        await _audit.LogAsync(adminId, c.IsActive ? "COUPON_ACTIVATE" : "COUPON_DEACTIVATE", "Coupon", id);
        return true;
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
        // Single batch query — no N+1 loop
        var disputes = await _db.Disputes
            .AsNoTracking()
            .Include(x => x.RaisedBy)
            .Include(x => x.Order)
            .Include(x => x.Messages).ThenInclude(m => m.Sender)
            .Where(d => d.RaisedByUserId == userId)
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync();
        return disputes.Select(MapDisputeDto).ToList();
    }

    public async Task<List<DisputeDetailDto>> GetAllDisputesAsync(DisputeStatus? status, int page)
    {
        // Single batch query — no N+1 loop
        var q = _db.Disputes.AsNoTracking()
            .Include(x => x.RaisedBy)
            .Include(x => x.Order)
            .Include(x => x.Messages).ThenInclude(m => m.Sender)
            .AsQueryable();
        if (status.HasValue) q = q.Where(d => d.Status == status.Value);
        var disputes = await q.OrderByDescending(d => d.CreatedAt)
            .Skip((page - 1) * 25).Take(25).ToListAsync();
        return disputes.Select(MapDisputeDto).ToList();
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
        var d = await _db.Disputes
            .Include(x => x.RaisedBy).Include(x => x.Order)
            .Include(x => x.Messages).ThenInclude(m => m.Sender)
            .FirstOrDefaultAsync(x => x.Id == id);
        return d == null ? null : MapDisputeDto(d);
    }

    private static DisputeDetailDto MapDisputeDto(Dispute d) =>
        new(d.Id, d.OrderId, d.Order.OrderNumber,
            d.RaisedBy.FullName, d.RaisedBy.Phone,
            d.Type, d.Type.ToString(), d.Status, d.Status.ToString(),
            d.Description, d.EvidenceUrl, d.Resolution, d.ResolutionNote,
            d.RefundAmount, d.AdminNotes, d.CreatedAt, d.ResolvedAt,
            d.Messages.OrderBy(m => m.SentAt)
                .Select(m => new DisputeMessageDto(m.Id, m.Sender.FullName,
                    m.IsAdminMessage, m.Message, m.SentAt)).ToList());
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

        // ── Server-side scalar aggregations (no full table scan) ─────
        var ordersToday    = await _db.Orders.CountAsync(o => o.CreatedAt >= today);
        var pendingPickups = await _db.Orders.CountAsync(o => o.Status == OrderStatus.RiderAssigned);
        var inProcessing   = await _db.Orders.CountAsync(o => o.Status == OrderStatus.Processing);
        var outForDelivery = await _db.Orders.CountAsync(o => o.Status == OrderStatus.OutForDelivery);
        var completedOrders = await _db.Orders.CountAsync(o => o.Status == OrderStatus.Completed);
        var activeDisputes = await _db.Disputes.CountAsync(d =>
            d.Status == DisputeStatus.Open || d.Status == DisputeStatus.UnderReview);
        var totalStores    = await _db.Stores.CountAsync(s => s.Status == StoreStatus.Active);
        var activeRiders   = await _db.Riders.CountAsync(r => r.Status == RiderStatus.OnlineAvailable);
        var totalCustomers = await _db.Users.CountAsync(u => u.Role == UserRole.Customer);

        // Revenue: EF translates ternary to CASE WHEN SQL expression
        var revenueToday = await _db.Orders
            .Where(o => o.CreatedAt >= today && o.PaymentState == PaymentState.FullyPaid)
            .SumAsync(o => (decimal?)(o.ActualTotal > 0 ? o.ActualTotal : o.TotalEstimate)) ?? 0m;
        var revenueWeek = await _db.Orders
            .Where(o => o.CreatedAt >= weekStart && o.PaymentState == PaymentState.FullyPaid)
            .SumAsync(o => (decimal?)(o.ActualTotal > 0 ? o.ActualTotal : o.TotalEstimate)) ?? 0m;
        var revenueMonth = await _db.Orders
            .Where(o => o.CreatedAt >= monthStart && o.PaymentState == PaymentState.FullyPaid)
            .SumAsync(o => (decimal?)(o.ActualTotal > 0 ? o.ActualTotal : o.TotalEstimate)) ?? 0m;

        // Daily metrics: one query for the whole week, then fill gaps in memory
        var weekEnd = weekStart.AddDays(7);
        var weekRaw = await _db.Orders
            .AsNoTracking()
            .Where(o => o.CreatedAt >= weekStart && o.CreatedAt < weekEnd)
            .Select(o => new {
                Day  = o.CreatedAt.Date,
                Paid = o.PaymentState == PaymentState.FullyPaid,
                Rev  = o.ActualTotal > 0 ? o.ActualTotal : o.TotalEstimate
            })
            .ToListAsync();

        var daily = Enumerable.Range(0, 7).Select(i =>
        {
            var d       = weekStart.AddDays(i);
            var dayRows = weekRaw.Where(r => r.Day == d).ToList();
            return new DailyMetric(d.ToString("ddd"), dayRows.Count,
                dayRows.Where(r => r.Paid).Sum(r => r.Rev));
        }).ToList();

        // Status breakdown: server-side GROUP BY
        var ordersByStatus = await _db.Orders
            .AsNoTracking()
            .GroupBy(o => o.Status)
            .Select(g => new StatusCount(g.Key.ToString(), g.Count()))
            .ToListAsync();

        return new DashboardSummaryDto(
            ordersToday, pendingPickups, inProcessing, outForDelivery, activeDisputes,
            revenueToday, revenueWeek, revenueMonth,
            totalStores, activeRiders, totalCustomers, completedOrders,
            daily, ordersByStatus);
    }
}

// ═══════════════════════════════════════════════════════════════════
// IMAGE PROCESSING SERVICE  (Task G)
// ═══════════════════════════════════════════════════════════════════
public interface IImageProcessingService
{
    /// <summary>
    /// Validates, resizes (max 1200 px on longest edge) and re-encodes the uploaded image.
    /// Returns the processed bytes in JPEG format, or throws if the file is invalid.
    /// Accepted types: jpg, jpeg, png, webp.
    /// Max input size: 10 MB.
    /// </summary>
    Task<(byte[] Bytes, string ContentType)> ProcessAsync(IFormFile file);
}

public class ImageProcessingService : IImageProcessingService
{
    private const int MaxInputBytes   = 10 * 1024 * 1024; // 10 MB hard limit
    private const int MaxDimension    = 1200;               // resize to fit within 1200 px
    private const int JpegQuality     = 82;                 // balanced quality/size

    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/jpg", "image/png", "image/webp"
    };

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp"
    };

    public async Task<(byte[] Bytes, string ContentType)> ProcessAsync(IFormFile file)
    {
        // ── 1. Basic validation ───────────────────────────────────
        if (file == null || file.Length == 0)
            throw new InvalidOperationException("No file uploaded.");

        if (file.Length > MaxInputBytes)
            throw new InvalidOperationException($"File too large (max {MaxInputBytes / 1024 / 1024} MB).");

        var ext = Path.GetExtension(file.FileName);
        if (!AllowedExtensions.Contains(ext))
            throw new InvalidOperationException("Unsupported file type. Accepted: jpg, jpeg, png, webp.");

        if (!AllowedContentTypes.Contains(file.ContentType))
            throw new InvalidOperationException("Invalid MIME type. Accepted: image/jpeg, image/png, image/webp.");

        // ── 2. Load, resize, re-encode ────────────────────────────
        using var inputStream = file.OpenReadStream();
        using var image = await Image.LoadAsync(inputStream);

        // Resize if either dimension exceeds 1200 px — maintain aspect ratio
        if (image.Width > MaxDimension || image.Height > MaxDimension)
        {
            var ratio  = Math.Min((double)MaxDimension / image.Width,
                                  (double)MaxDimension / image.Height);
            var newW   = (int)Math.Round(image.Width  * ratio);
            var newH   = (int)Math.Round(image.Height * ratio);
            image.Mutate(ctx => ctx.Resize(newW, newH));
        }

        using var output = new MemoryStream();
        var encoder = new JpegEncoder { Quality = JpegQuality };
        await image.SaveAsync(output, encoder);

        return (output.ToArray(), "image/jpeg");
    }
}
