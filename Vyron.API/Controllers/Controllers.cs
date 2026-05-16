using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Vyron.API.Data;
using Vyron.API.DTOs;
using Vyron.API.Models;
using Vyron.API.Services;
using Vyron.Shared.Enums;

namespace Vyron.API.Controllers;

[ApiController]
public abstract class VyronController : ControllerBase
{
    protected Guid CurrentUserId =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : Guid.Empty;
    protected string CurrentUserRole => User.FindFirstValue(ClaimTypes.Role) ?? "";
    protected bool IsAdmin => CurrentUserRole is "Admin" or "SuperAdmin";
}

// ─── AUTH ─────────────────────────────────────────────────────────
[Route("api/auth")]
public class AuthController : VyronController
{
    private readonly IAuthService _auth;
    public AuthController(IAuthService auth) => _auth = auth;

    [HttpPost("send-otp"), AllowAnonymous]
    public async Task<IActionResult> SendOtp([FromBody] SendOtpRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Phone))
            return BadRequest(new SendOtpResponse(false, "Phone is required."));

        var (ok, msg, devOtp) = await _auth.SendOtpAsync(req.Phone);
        // devOtp is non-null only when Otp:ReturnOtpInDevelopment=true in config.
        // In production devOtp will always be null — safe to include in response.
        return ok
            ? Ok(new SendOtpResponse(true, msg, devOtp))
            : BadRequest(new SendOtpResponse(false, msg));
    }

    [HttpPost("verify-otp"), AllowAnonymous]
    public async Task<IActionResult> VerifyOtp([FromBody] VerifyOtpRequest req)
    {
        var result = await _auth.VerifyOtpAsync(req);
        return result != null ? Ok(result) : BadRequest(new { message = "Invalid or expired OTP." });
    }

    [HttpPost("refresh-token"), AllowAnonymous]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequest req)
    {
        var result = await _auth.RefreshTokenAsync(req.RefreshToken);
        return result != null ? Ok(result) : Unauthorized();
    }

    [HttpPost("logout"), Authorize]
    public async Task<IActionResult> Logout([FromBody] RefreshTokenRequest req)
    {
        await _auth.RevokeTokenAsync(req.RefreshToken);
        return Ok(new { message = "Logged out." });
    }
}

// ─── STORES ───────────────────────────────────────────────────────
[Route("api/stores")]
public class StoresController : VyronController
{
    private readonly IStoreService _stores;
    public StoresController(IStoreService stores) => _stores = stores;

    [HttpGet, AllowAnonymous]
    public async Task<IActionResult> GetAll([FromQuery] string? search, [FromQuery] string? sort,
        [FromQuery] string? filter, [FromQuery] double? lat, [FromQuery] double? lng)
        => Ok(await _stores.GetStoresAsync(search, sort, filter, lat, lng));

    [HttpGet("{id:guid}"), AllowAnonymous]
    public async Task<IActionResult> GetOne(Guid id)
    {
        var store = await _stores.GetStoreAsync(id);
        return store != null ? Ok(store) : NotFound();
    }

    [HttpPost, Authorize(Roles = "StoreOwner,Admin,SuperAdmin")]
    public async Task<IActionResult> Create([FromBody] CreateStoreRequest req)
    {
        var store = await _stores.CreateStoreAsync(CurrentUserId, req);
        return CreatedAtAction(nameof(GetOne), new { id = store.Id }, store);
    }

    [HttpPut("{id:guid}"), Authorize(Roles = "StoreOwner,Admin,SuperAdmin")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateStoreRequest req)
    {
        var store = await _stores.UpdateStoreAsync(id, req);
        return store != null ? Ok(store) : NotFound();
    }

    [HttpGet("{id:guid}/reviews"), AllowAnonymous]
    public async Task<IActionResult> GetReviews(Guid id, [FromServices] IReviewService reviews, [FromQuery] int page = 1)
        => Ok(await reviews.GetStoreReviewsAsync(id, page));

    [HttpPost("{id:guid}/services"), Authorize(Roles = "StoreOwner,Admin,SuperAdmin")]
    public async Task<IActionResult> AddService(Guid id, [FromBody] UpsertServiceRequest req)
        => Ok(await _stores.UpsertServiceAsync(id, req));
}

// ─── ORDERS (customer-facing) ─────────────────────────────────────
[Route("api/orders"), Authorize]
public class OrdersController : VyronController
{
    private readonly IOrderService _orders;
    public OrdersController(IOrderService orders) => _orders = orders;

    [HttpPost("estimate"), AllowAnonymous]
    public async Task<IActionResult> Estimate([FromBody] PriceEstimateRequest req)
        => Ok(await _orders.EstimatePriceAsync(req));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateOrderRequest req)
    {
        var order = await _orders.CreateOrderAsync(CurrentUserId, req);
        return CreatedAtAction(nameof(GetOrder), new { id = order.Id }, order);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetOrder(Guid id)
    {
        var order = await _orders.GetOrderAsync(id);
        return order != null ? Ok(order) : NotFound();
    }

    [HttpGet("track/{number}"), AllowAnonymous]
    public async Task<IActionResult> Track(string number)
    {
        var order = await _orders.GetOrderByNumberAsync(number);
        return order != null ? Ok(order) : NotFound();
    }

    [HttpGet("my-orders")]
    public async Task<IActionResult> MyOrders([FromQuery] int page = 1)
        => Ok(await _orders.GetCustomerOrdersAsync(CurrentUserId, page));
}

// ─── ADMIN ORDERS ─────────────────────────────────────────────────
[Route("api/admin/orders"), Authorize(Roles = "Admin,SuperAdmin")]
public class AdminOrdersController : VyronController
{
    private readonly IOrderService _orders;
    public AdminOrdersController(IOrderService orders) => _orders = orders;

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] OrderStatus? status,
        [FromQuery] string? search, [FromQuery] int page = 1)
        => Ok(await _orders.GetAllOrdersAsync(status, search, page));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetOne(Guid id)
    {
        var o = await _orders.GetOrderAsync(id);
        return o != null ? Ok(o) : NotFound();
    }

    [HttpPut("{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateOrderStatusRequest req)
    {
        var o = await _orders.UpdateStatusAsync(id, req.Status, req.Note, CurrentUserId);
        return o != null ? Ok(o) : NotFound();
    }

    [HttpPut("{id:guid}/assign-rider")]
    public async Task<IActionResult> AssignRider(Guid id, [FromBody] AssignRiderRequest req)
    {
        var o = await _orders.AssignRiderAsync(id, req.RiderId, CurrentUserId);
        return o != null ? Ok(o) : NotFound();
    }

    [HttpPut("{id:guid}/override-price")]
    public async Task<IActionResult> Override(Guid id, [FromBody] OverridePriceRequest req)
    {
        var o = await _orders.OverridePriceAsync(id, req, CurrentUserId);
        return o != null ? Ok(o) : NotFound();
    }
}

// ─── STORE OWNER ───────────────────────────────────────────────────
[Route("api/store-owner"), Authorize(Roles = "StoreOwner,Admin,SuperAdmin")]
public class StoreOwnerController : VyronController
{
    private readonly IOrderService _orders;
    private readonly IStoreService _stores;
    private readonly VyronDbContext _db;

    public StoreOwnerController(IOrderService orders, IStoreService stores, VyronDbContext db)
    { _orders = orders; _stores = stores; _db = db; }

    [HttpGet("my-stores")]
    public async Task<IActionResult> MyStores()
    {
        var stores = await _db.Stores.Include(s => s.Services)
            .Where(s => s.OwnerId == CurrentUserId).ToListAsync();
        return Ok(stores.Select(s => new { s.Id, s.Name, s.Area, s.Status,
            s.AverageRating, s.TotalOrders, s.TotalReviews,
            ActiveServices = s.Services.Count(svc => svc.IsActive) }));
    }

    [HttpGet("stores/{storeId:guid}/orders")]
    public async Task<IActionResult> StoreOrders(Guid storeId, [FromQuery] int page = 1)
    {
        var store = await _db.Stores.FirstOrDefaultAsync(s => s.Id == storeId);
        if (store == null) return NotFound();
        if (store.OwnerId != CurrentUserId && !IsAdmin) return Forbid();
        return Ok(await _orders.GetStoreOrdersAsync(storeId, page));
    }

    [HttpPut("orders/{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateOrderStatusRequest req)
    {
        var order = await _db.Orders.Include(o => o.Store).FirstOrDefaultAsync(o => o.Id == id);
        if (order == null) return NotFound();
        if (order.Store.OwnerId != CurrentUserId && !IsAdmin) return Forbid();

        // Store owners can only set: Confirmed, Processing, Ready
        if (!IsAdmin && req.Status is not (OrderStatus.Confirmed or OrderStatus.Processing or OrderStatus.Ready))
            return BadRequest(new { message = "Store owners can only set: Confirmed, Processing, Ready." });

        var result = await _orders.UpdateStatusAsync(id, req.Status, req.Note, CurrentUserId);
        return result != null ? Ok(result) : NotFound();
    }
}

// ─── RIDER ────────────────────────────────────────────────────────
[Route("api/rider"), Authorize(Roles = "Rider")]
public class RiderController : VyronController
{
    private readonly IOrderService _orders;
    private readonly VyronDbContext _db;

    public RiderController(IOrderService orders, VyronDbContext db)
    { _orders = orders; _db = db; }

    [HttpGet("my-orders")]
    public async Task<IActionResult> GetMyOrders()
    {
        var rider = await _db.Riders.FirstOrDefaultAsync(r => r.UserId == CurrentUserId);
        if (rider == null) return NotFound();
        return Ok(await _orders.GetRiderOrdersAsync(rider.Id));
    }

    [HttpPut("orders/{id:guid}/pickup")]
    public async Task<IActionResult> MarkPickup(Guid id)
    {
        var o = await _orders.UpdateStatusAsync(id, OrderStatus.PickedUp, "Picked up by rider", CurrentUserId);
        return o != null ? Ok(o) : NotFound();
    }

    [HttpPut("orders/{id:guid}/deliver")]
    public async Task<IActionResult> MarkDelivered(Guid id)
    {
        var o = await _orders.UpdateStatusAsync(id, OrderStatus.Delivered, "Delivered to customer", CurrentUserId);
        return o != null ? Ok(o) : NotFound();
    }
}

// ─── DISPUTES ─────────────────────────────────────────────────────
[Route("api/disputes"), Authorize]
public class DisputesController : VyronController
{
    private readonly IDisputeService _disputes;
    public DisputesController(IDisputeService disputes) => _disputes = disputes;

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateDisputeRequest req)
    {
        var d = await _disputes.CreateDisputeAsync(CurrentUserId, req);
        return CreatedAtAction(nameof(GetOne), new { id = d.Id }, d);
    }

    [HttpGet("my")]
    public async Task<IActionResult> GetMine()
    {
        var disputes = await _disputes.GetMyDisputesAsync(CurrentUserId);
        return Ok(disputes);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetOne(Guid id)
    {
        var d = await _disputes.GetDisputeAsync(id);
        return d != null ? Ok(d) : NotFound();
    }

    [HttpPost("{id:guid}/messages")]
    public async Task<IActionResult> AddMessage(Guid id, [FromBody] AddDisputeMessageRequest req)
    {
        var msg = await _disputes.AddMessageAsync(id, CurrentUserId, req.Message, IsAdmin);
        return Ok(msg);
    }
}

[Route("api/admin/disputes"), Authorize(Roles = "Admin,SuperAdmin")]
public class AdminDisputesController : VyronController
{
    private readonly IDisputeService _disputes;
    public AdminDisputesController(IDisputeService disputes) => _disputes = disputes;

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] DisputeStatus? status, [FromQuery] int page = 1)
        => Ok(await _disputes.GetAllDisputesAsync(status, page));

    [HttpPut("{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateOrderStatusRequest req)
    {
        var d = await _disputes.UpdateStatusAsync(id, (DisputeStatus)(int)req.Status, req.Note, CurrentUserId);
        return d != null ? Ok(d) : NotFound();
    }

    [HttpPut("{id:guid}/resolve")]
    public async Task<IActionResult> Resolve(Guid id, [FromBody] ResolveDisputeRequest req)
    {
        var d = await _disputes.ResolveAsync(id, req, CurrentUserId);
        return d != null ? Ok(d) : NotFound();
    }
}

// ─── REVIEWS ──────────────────────────────────────────────────────
[Route("api/reviews"), Authorize]
public class ReviewsController : VyronController
{
    private readonly IReviewService _reviews;
    public ReviewsController(IReviewService reviews) => _reviews = reviews;

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateReviewRequest req)
        => Ok(await _reviews.CreateReviewAsync(CurrentUserId, req));
}

[Route("api/admin/reviews"), Authorize(Roles = "Admin,SuperAdmin")]
public class AdminReviewsController : VyronController
{
    private readonly IReviewService _reviews;
    public AdminReviewsController(IReviewService reviews) => _reviews = reviews;

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] int page = 1)
        => Ok(await _reviews.GetAllReviewsAsync(page));

    [HttpPut("{id:guid}/moderate")]
    public async Task<IActionResult> Moderate(Guid id, [FromQuery] bool visible, [FromQuery] string? note)
    {
        await _reviews.ModerateReviewAsync(id, visible, note, CurrentUserId);
        return Ok();
    }
}

// ─── PAYMENTS ─────────────────────────────────────────────────────
[Route("api/payments"), Authorize]
public class PaymentsController : VyronController
{
    private readonly IPaymentService _payments;
    public PaymentsController(IPaymentService payments) => _payments = payments;

    [HttpPost]
    public async Task<IActionResult> Record([FromBody] RecordPaymentRequest req)
        => Ok(await _payments.RecordPaymentAsync(req, CurrentUserId));

    [HttpGet("order/{orderId:guid}")]
    public async Task<IActionResult> GetOrderPayments(Guid orderId)
        => Ok(await _payments.GetOrderPaymentsAsync(orderId));
}

// ─── ADMIN ────────────────────────────────────────────────────────
[Route("api/admin"), Authorize(Roles = "Admin,SuperAdmin")]
public class AdminController : VyronController
{
    private readonly IAnalyticsService _analytics;
    private readonly VyronDbContext _db;

    public AdminController(IAnalyticsService analytics, VyronDbContext db)
    { _analytics = analytics; _db = db; }

    [HttpGet("dashboard")]
    public async Task<IActionResult> Dashboard() => Ok(await _analytics.GetDashboardAsync());

    [HttpGet("riders")]
    public async Task<IActionResult> GetRiders()
    {
        var riders = await _db.Riders.Include(r => r.User).Include(r => r.Orders).ToListAsync();
        return Ok(riders.Select(r => new RiderDto(r.Id, r.UserId, r.User.FullName,
            r.User.Phone, r.VehicleType, r.VehiclePlate, r.Status,
            r.CurrentLatitude, r.CurrentLongitude, r.TotalDeliveries, r.TotalEarnings,
            r.Orders.Count(o => o.Status != OrderStatus.Completed && o.Status != OrderStatus.Cancelled))));
    }

    [HttpGet("config")]
    public async Task<IActionResult> GetConfig()
    {
        var configs = await _db.SystemConfigs.ToListAsync();
        return Ok(configs.Select(c => new ConfigDto(c.Id, c.Key, c.Value, c.Description, c.UpdatedAt)));
    }

    [HttpPut("config/{key}")]
    public async Task<IActionResult> UpdateConfig(string key, [FromBody] UpdateConfigRequest req)
    {
        var cfg = await _db.SystemConfigs.FirstOrDefaultAsync(c => c.Key == key);
        if (cfg == null) return NotFound();
        cfg.Value = req.Value; cfg.UpdatedAt = DateTime.UtcNow; cfg.UpdatedByUserId = CurrentUserId;
        await _db.SaveChangesAsync();
        return Ok(new ConfigDto(cfg.Id, cfg.Key, cfg.Value, cfg.Description, cfg.UpdatedAt));
    }

    [HttpGet("customers")]
    public async Task<IActionResult> GetCustomers([FromQuery] string? search, [FromQuery] int page = 1)
    {
        var q = _db.Users.Include(u => u.Orders).Where(u => u.Role == UserRole.Customer).AsQueryable();
        if (!string.IsNullOrEmpty(search))
            q = q.Where(u => u.FullName.Contains(search) || u.Phone.Contains(search));
        var users = await q.OrderByDescending(u => u.CreatedAt)
            .Skip((page - 1) * 25).Take(25).ToListAsync();
        return Ok(users.Select(u => new
        {
            u.Id, u.FullName, u.Phone, u.Email,
            TotalOrders = u.Orders.Count,
            TotalSpend = u.Orders.Where(o => o.PaymentState == PaymentState.FullyPaid).Sum(o => o.TotalEstimate),
            u.IsActive, u.CreatedAt
        }));
    }
}

// ─── CUSTOMER PROFILE ─────────────────────────────────────────────
[Route("api/profile"), Authorize]
public class ProfileController : VyronController
{
    private readonly VyronDbContext _db;
    public ProfileController(VyronDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetProfile()
    {
        var user = await _db.Users.FindAsync(CurrentUserId);
        if (user == null) return NotFound();
        return Ok(new ProfileDto(user.Id, user.FullName, user.Phone, user.Email,
            user.Role, user.ProfilePhoto, user.CreatedAt));
    }

    [HttpPut]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest req)
    {
        var user = await _db.Users.FindAsync(CurrentUserId);
        if (user == null) return NotFound();
        if (!string.IsNullOrWhiteSpace(req.FullName)) user.FullName = req.FullName.Trim();
        if (req.Email != null) user.Email = req.Email.Trim();
        await _db.SaveChangesAsync();
        return Ok(new ProfileDto(user.Id, user.FullName, user.Phone, user.Email,
            user.Role, user.ProfilePhoto, user.CreatedAt));
    }
}

// ─── NOTIFICATIONS ────────────────────────────────────────────────
[Route("api/notifications"), Authorize]
public class NotificationsController : VyronController
{
    private readonly VyronDbContext _db;
    public NotificationsController(VyronDbContext db) => _db = db;

    /// <summary>Get current user's notifications, newest first (max 50).</summary>
    [HttpGet]
    public async Task<IActionResult> GetMyNotifications()
    {
        var notifications = await _db.Notifications
            .Where(n => n.UserId == CurrentUserId)
            .OrderByDescending(n => n.CreatedAt)
            .Take(50)
            .Select(n => new NotificationDto(n.Id, n.Title, n.Message, n.Type, n.IsRead, n.CreatedAt))
            .ToListAsync();
        return Ok(notifications);
    }

    /// <summary>Mark a notification as read.</summary>
    [HttpPost("{id:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid id)
    {
        var notification = await _db.Notifications
            .FirstOrDefaultAsync(n => n.Id == id && n.UserId == CurrentUserId);
        if (notification == null) return NotFound();
        notification.IsRead = true;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>Mark all of the current user's notifications as read.</summary>
    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllRead()
    {
        await _db.Notifications
            .Where(n => n.UserId == CurrentUserId && !n.IsRead)
            .ExecuteUpdateAsync(setters => setters.SetProperty(n => n.IsRead, true));
        return NoContent();
    }
}

// ─── RIDER MESSAGE ────────────────────────────────────────────────
[Route("api/orders"), Authorize]
public class OrderRiderMessageController : VyronController
{
    private readonly VyronDbContext _db;
    private readonly INotificationService _notifications;
    public OrderRiderMessageController(VyronDbContext db, INotificationService notifications)
    { _db = db; _notifications = notifications; }

    /// <summary>Customer sends a message to their assigned rider for an active order.</summary>
    [HttpPost("{orderId:guid}/rider-message")]
    public async Task<IActionResult> SendRiderMessage(Guid orderId, [FromBody] SendRiderMessageRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Message))
            return BadRequest(new { error = "Message cannot be empty." });

        var order = await _db.Orders
            .Include(o => o.Customer)
            .Include(o => o.Rider).ThenInclude(r => r!.User)
            .FirstOrDefaultAsync(o => o.Id == orderId && o.CustomerId == CurrentUserId);

        if (order == null) return NotFound();
        if (order.Rider == null) return BadRequest(new { error = "No rider assigned to this order yet." });

        // Log the message as a CommunicationLog entry
        _db.CommunicationLogs.Add(new CommunicationLog
        {
            Channel           = "InApp",
            Status            = "Sent",
            RecipientUserId   = order.Rider.UserId,
            RecipientName     = order.Rider.User.FullName,
            RecipientPhone    = order.Rider.User.Phone,
            Subject           = $"Message from customer for order #{order.OrderNumber}",
            Body              = req.Message.Trim(),
            RelatedEntityType = "Order",
            RelatedEntityId   = orderId,
            SentByAdminId     = null,
            SentAt            = DateTime.UtcNow,
            CreatedAt         = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        // Send in-app notification to the rider
        await _notifications.SendInAppAsync(order.Rider.UserId,
            $"Message from {order.Customer.FullName.Split(' ')[0]}",
            $"Order #{order.OrderNumber}: {req.Message.Trim()}",
            "message");

        return Ok(new { success = true, message = "Message sent to rider." });
    }
}
