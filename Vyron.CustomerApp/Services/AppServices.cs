using Vyron.CustomerApp.DTOs;

namespace Vyron.CustomerApp.Services;

// ═══════════════════════════════════════════════════════════════════
// STORE SERVICE
// ═══════════════════════════════════════════════════════════════════
public class StoreService
{
    private readonly ApiService _api;
    public StoreService(ApiService api) => _api = api;

    public Task<(List<StoreListItemDto>? Data, string? Error)> GetStoresAsync(
        string? search = null, string? sort = null, string? filter = null)
    {
        var qs = BuildQuery(("search", search), ("sort", sort), ("filter", filter));
        return _api.GetAsync<List<StoreListItemDto>>($"api/stores{qs}");
    }

    public Task<(StoreDetailDto? Data, string? Error)> GetStoreAsync(Guid id)
        => _api.GetAsync<StoreDetailDto>($"api/stores/{id}");

    public Task<(List<ReviewDto>? Data, string? Error)> GetReviewsAsync(Guid storeId, int page = 1)
        => _api.GetAsync<List<ReviewDto>>($"api/stores/{storeId}/reviews?page={page}");

    private static string BuildQuery(params (string Key, string? Value)[] pairs)
    {
        var parts = pairs.Where(p => !string.IsNullOrEmpty(p.Value))
                         .Select(p => $"{p.Key}={Uri.EscapeDataString(p.Value!)}");
        var qs = string.Join("&", parts);
        return qs.Length > 0 ? $"?{qs}" : "";
    }
}

// ═══════════════════════════════════════════════════════════════════
// ORDER SERVICE
// ═══════════════════════════════════════════════════════════════════
public class OrderService
{
    private readonly ApiService _api;
    public OrderService(ApiService api) => _api = api;

    public Task<(PriceEstimateResponse? Data, string? Error)> EstimateAsync(PriceEstimateRequest req)
        => _api.PostAsync<PriceEstimateResponse>("api/orders/estimate", req);

    public Task<(OrderDto? Data, string? Error)> CreateOrderAsync(CreateOrderRequest req)
        => _api.PostAsync<OrderDto>("api/orders", req);

    public Task<(OrderDto? Data, string? Error)> GetOrderAsync(Guid id)
        => _api.GetAsync<OrderDto>($"api/orders/{id}");

    public Task<(OrderDto? Data, string? Error)> TrackByNumberAsync(string number)
        => _api.GetAsync<OrderDto>($"api/orders/track/{Uri.EscapeDataString(number)}");

    public Task<(List<OrderDto>? Data, string? Error)> GetMyOrdersAsync(int page = 1)
        => _api.GetAsync<List<OrderDto>>($"api/orders/my-orders?page={page}");
}

// ═══════════════════════════════════════════════════════════════════
// PAYMENT SERVICE
// ═══════════════════════════════════════════════════════════════════
public class PaymentService
{
    private readonly ApiService _api;
    public PaymentService(ApiService api) => _api = api;

    /// <summary>Record pickup fee payment. Method: CashOnDelivery | Transfer</summary>
    public Task<(PaymentDto? Data, string? Error)> PayPickupFeeAsync(
        Guid orderId, decimal amount, string method = "CashOnDelivery")
        => _api.PostAsync<PaymentDto>("api/payments", new RecordPaymentRequest
        {
            OrderId = orderId,
            Amount  = amount,
            Method  = method,
            Type    = "PickupFee",
            Notes   = "Pickup fee recorded via customer app"
        });

    /// <summary>Record balance payment after delivery.</summary>
    public Task<(PaymentDto? Data, string? Error)> PayBalanceAsync(
        Guid orderId, decimal amount, string method = "CashOnDelivery")
        => _api.PostAsync<PaymentDto>("api/payments", new RecordPaymentRequest
        {
            OrderId = orderId,
            Amount  = amount,
            Method  = method,
            Type    = "Balance",
            Notes   = "Balance paid via customer app"
        });

    public Task<(List<PaymentDto>? Data, string? Error)> GetOrderPaymentsAsync(Guid orderId)
        => _api.GetAsync<List<PaymentDto>>($"api/payments/order/{orderId}");
}

// ═══════════════════════════════════════════════════════════════════
// DISPUTE SERVICE
// ═══════════════════════════════════════════════════════════════════
public class DisputeService
{
    private readonly ApiService _api;
    public DisputeService(ApiService api) => _api = api;

    public Task<(DisputeDetailDto? Data, string? Error)> CreateAsync(CreateDisputeRequest req)
        => _api.PostAsync<DisputeDetailDto>("api/disputes", req);

    public Task<(DisputeDetailDto? Data, string? Error)> GetAsync(Guid id)
        => _api.GetAsync<DisputeDetailDto>($"api/disputes/{id}");

    public Task<(List<DisputeDetailDto>? Data, string? Error)> GetMyDisputesAsync()
        => _api.GetAsync<List<DisputeDetailDto>>("api/disputes/my");

    public Task<(object? Data, string? Error)> AddMessageAsync(Guid id, string message)
        => _api.PostAsync<object>($"api/disputes/{id}/messages", new { Message = message });
}

// ═══════════════════════════════════════════════════════════════════
// REVIEW SERVICE
// ═══════════════════════════════════════════════════════════════════
public class ReviewService
{
    private readonly ApiService _api;
    public ReviewService(ApiService api) => _api = api;

    public Task<(ReviewDto? Data, string? Error)> CreateAsync(CreateReviewRequest req)
        => _api.PostAsync<ReviewDto>("api/reviews", req);
}

// ═══════════════════════════════════════════════════════════════════
// NOTIFICATION SERVICE
// ═══════════════════════════════════════════════════════════════════
public class NotificationService
{
    private readonly ApiService _api;
    public NotificationService(ApiService api) => _api = api;

    public Task<(List<NotificationDto>? Data, string? Error)> GetMyNotificationsAsync()
        => _api.GetAsync<List<NotificationDto>>("api/notifications");

    public Task<(object? Data, string? Error)> MarkReadAsync(Guid id)
        => _api.PostAsync<object>($"api/notifications/{id}/read", new { });

    public Task<(object? Data, string? Error)> MarkAllReadAsync()
        => _api.PostAsync<object>("api/notifications/read-all", new { });
}

// ═══════════════════════════════════════════════════════════════════
// RIDER MESSAGE SERVICE
// ═══════════════════════════════════════════════════════════════════
public class RiderMessageService
{
    private readonly ApiService _api;
    public RiderMessageService(ApiService api) => _api = api;

    public Task<(object? Data, string? Error)> SendAsync(Guid orderId, string message)
        => _api.PostAsync<object>($"api/orders/{orderId}/rider-message",
            new SendRiderMessageRequest(message));
}

// ═══════════════════════════════════════════════════════════════════
// PROFILE SERVICE
// ═══════════════════════════════════════════════════════════════════
public class ProfileService
{
    private readonly ApiService _api;
    public ProfileService(ApiService api) => _api = api;

    public Task<(ProfileDto? Data, string? Error)> GetProfileAsync()
        => _api.GetAsync<ProfileDto>("api/profile");

    public Task<(ProfileDto? Data, string? Error)> UpdateProfileAsync(string? fullName, string? email)
        => _api.PutAsync<ProfileDto>("api/profile", new UpdateProfileRequest(fullName, email));
}
