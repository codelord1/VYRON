using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Globalization;
using Vyron.CustomerApp.DTOs;

namespace Vyron.CustomerApp.Services;

// ─── DRAFT ITEM (one selected service + qty in the cart) ─────────
public partial class OrderDraftItem : ObservableObject
{
    public Guid   ServiceId   { get; init; }
    public string ServiceName { get; init; } = "";
    public string PricingMode { get; init; } = "PerKg";   // "PerKg" | "PerItem" | "Fixed"
    public decimal BasePrice  { get; init; }
    public decimal MinimumCharge { get; init; }

    public bool IsPerKg => PricingMode.Equals("PerKg", StringComparison.OrdinalIgnoreCase);
    public bool IsPerItem => PricingMode.Equals("PerItem", StringComparison.OrdinalIgnoreCase);
    public bool IsFixed => PricingMode.Equals("Fixed", StringComparison.OrdinalIgnoreCase);
    public bool HasMinimumCharge => IsPerKg && MinimumCharge > 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LineTotal), nameof(LineTotalDisplay), nameof(QtyDisplay), nameof(LineSummary))]
    private decimal _weight = 1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LineTotal), nameof(LineTotalDisplay), nameof(QtyDisplay), nameof(LineSummary))]
    private int _pieces = 1;

    // ── String wrappers for Entry two-way binding ─────────────────
    // Using string intermediaries avoids decimal/int ↔ string coercion problems in MAUI Entry.

    private string _weightText = "1.0";
    /// <summary>Two-way string binding for the "Weight (kg)" Entry.</summary>
    public string WeightText
    {
        get => _weightText;
        set
        {
            if (SetProperty(ref _weightText, value))
            {
                if (decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var w) && w > 0)
                    Weight = w;
            }
        }
    }

    private string _piecesText = "1";
    /// <summary>Two-way string binding for the "Pieces" Entry.</summary>
    public string PiecesText
    {
        get => _piecesText;
        set
        {
            if (SetProperty(ref _piecesText, value))
            {
                if (int.TryParse(value, out var p) && p > 0)
                    Pieces = p;
            }
        }
    }

    // Keep text in sync when Weight/Pieces change from AddService increments
    partial void OnWeightChanged(decimal value)
    {
        var formatted = value.ToString("F1", CultureInfo.InvariantCulture);
        if (_weightText != formatted) { _weightText = formatted; OnPropertyChanged(nameof(WeightText)); }
    }
    partial void OnPiecesChanged(int value)
    {
        var formatted = value.ToString();
        if (_piecesText != formatted) { _piecesText = formatted; OnPropertyChanged(nameof(PiecesText)); }
    }

    public decimal LineTotal
    {
        get
        {
            if (IsPerKg)
                return MinimumCharge > 0 ? Math.Max(Weight * BasePrice, MinimumCharge) : Weight * BasePrice;
            if (IsPerItem)
                return Pieces * BasePrice;
            return BasePrice;
        }
    }

    public string LineTotalDisplay => $"₦{LineTotal:N0}";

    public string QtyDisplay
    {
        get
        {
            if (IsPerKg) return $"{Weight:F1} kg";
            if (IsPerItem) return $"{Pieces} items";
            return "Fixed";
        }
    }

    public string PricingModeLabel
    {
        get
        {
            if (IsPerKg) return "Per kg";
            if (IsPerItem) return "Per item";
            return "Fixed";
        }
    }

    public string PriceDisplay
    {
        get
        {
            if (IsPerKg) return $"₦{BasePrice:N0}/kg";
            if (IsPerItem) return $"₦{BasePrice:N0}/item";
            return $"₦{BasePrice:N0} fixed";
        }
    }

    public string MinimumChargeDisplay => $"Minimum charge: ₦{MinimumCharge:N0}";

    public string LineSummary
    {
        get
        {
            if (IsPerKg) return $"{ServiceName} - {Weight:F1}kg x ₦{BasePrice:N0}/kg = {LineTotalDisplay}";
            if (IsPerItem) return $"{ServiceName} - {Pieces} items x ₦{BasePrice:N0}/item = {LineTotalDisplay}";
            return $"{ServiceName} - fixed price = {LineTotalDisplay}";
        }
    }

    public bool IsValid => (IsPerKg && Weight >= 0.5m && BasePrice > 0)
        || (IsPerItem && Pieces >= 1 && BasePrice > 0)
        || (IsFixed && BasePrice > 0);

    // ── Stepper commands ─────────────────────────────────────────────
    [RelayCommand]
    public void IncrementWeight()
    {
        Weight = Math.Round(Weight + 0.5m, 1);
    }

    [RelayCommand]
    public void DecrementWeight()
    {
        if (Weight > 0.5m) Weight = Math.Round(Weight - 0.5m, 1);
    }

    [RelayCommand]
    public void IncrementPieces()
    {
        Pieces++;
    }

    [RelayCommand]
    public void DecrementPieces()
    {
        if (Pieces > 1) Pieces--;
    }

    /// Convert to the API request item.
    public CreateOrderItemRequest ToRequest() => new()
    {
        ServiceOfferingId = ServiceId,
        Weight = IsPerKg ? Weight : 0,
        Pieces = IsPerItem ? Pieces : IsFixed ? 1 : 0
    };
}

// ─── ORDER DRAFT SERVICE (singleton cart / draft holder) ─────────
/// <summary>
/// Singleton that holds the in-progress order across the
/// ServiceSelection → CreateOrder → PickupFeePayment navigation flow.
/// Injected into ViewModels as a singleton so state survives page transitions.
/// </summary>
public class OrderDraftService
{
    // ── Store context ────────────────────────────────────────────────
    public Guid    StoreId    { get; private set; }
    public string  StoreName  { get; private set; } = "";
    public decimal PickupFee  { get; private set; }
    public decimal DeliveryFee{ get; private set; }

    // ── Selected line items ──────────────────────────────────────────
    public ObservableCollection<OrderDraftItem> Items { get; } = new();

    // ── Computed totals ──────────────────────────────────────────────
    public decimal TotalLaundryCost => Items.Sum(i => i.LineTotal);
    public decimal TotalEstimate    => TotalLaundryCost + PickupFee + DeliveryFee;
    public decimal AmountDueNow     => PickupFee;
    public decimal BalanceDueLater  => TotalEstimate - AmountDueNow;
    public bool    HasItems         => Items.Count > 0;

    // ── Summary text for checkout ───────────────────────────────────
    public string Breakdown => string.Join("\n",
        Items.Select(i => $"{i.ServiceName} ({i.QtyDisplay}) = {i.LineTotalDisplay}"));

    // ── Methods ──────────────────────────────────────────────────────

    /// <summary>Call once when the customer enters the service-selection page.</summary>
    public void SetStore(Guid storeId, string name, decimal pickupFee, decimal deliveryFee)
    {
        if (storeId != StoreId)
        {
            StoreId     = storeId;
            StoreName   = name;
            PickupFee   = pickupFee;
            DeliveryFee = deliveryFee;
            Items.Clear();
        }
    }

    /// <summary>
    /// Add a service to the cart. If the same service is already in the cart
    /// the relevant customer-facing quantity is incremented instead.
    /// </summary>
    public void AddService(ServiceSummaryDto svc)
    {
        var existing = Items.FirstOrDefault(i => i.ServiceId == svc.Id);
        if (existing != null)
        {
            if (existing.IsPerKg)
                existing.Weight = Math.Round(existing.Weight + 0.5m, 1);
            else if (existing.IsPerItem)
                existing.Pieces += 1;
        }
        else
        {
            Items.Add(new OrderDraftItem
            {
                ServiceId    = svc.Id,
                ServiceName  = svc.Name,
                PricingMode  = svc.PricingMode,
                BasePrice    = svc.BasePrice,
                MinimumCharge= svc.MinimumCharge,
                Weight       = 1,
                Pieces       = 1
            });
        }
    }

    /// <summary>Remove an item from the cart.</summary>
    public void RemoveItem(OrderDraftItem item) => Items.Remove(item);

    /// <summary>Returns true if the given service id is already in the cart.</summary>
    public bool ContainsService(Guid serviceId) => Items.Any(i => i.ServiceId == serviceId);

    /// <summary>Returns how many units of a given service are in the cart (weight or pieces).</summary>
    public string ServiceQtyDisplay(Guid serviceId)
    {
        var item = Items.FirstOrDefault(i => i.ServiceId == serviceId);
        return item?.QtyDisplay ?? "";
    }

    /// <summary>Clear the whole draft (call after order is placed or user navigates away).</summary>
    public void Clear()
    {
        Items.Clear();
        StoreId     = Guid.Empty;
        StoreName   = "";
        PickupFee   = 0;
        DeliveryFee = 0;
    }

    // ── Build the CreateOrderRequest ─────────────────────────────────
    public CreateOrderRequest BuildRequest(
        string pickupAddress, string deliveryAddress,
        DateTime pickupDate, string pickupSlot,
        string paymentMethod, string? notes = null)
    {
        var primary = Items.FirstOrDefault();
        return new CreateOrderRequest
        {
            StoreId              = StoreId,
            ServiceOfferingId    = primary?.ServiceId ?? Guid.Empty,
            EstimatedWeight      = Items.Where(i => i.IsPerKg).Sum(i => i.Weight),
            EstimatedPieces      = Items.Where(i => i.IsPerItem || i.IsFixed).Sum(i => i.ToRequest().Pieces),
            PickupAddress        = pickupAddress,
            DeliveryAddress      = deliveryAddress,
            RequestedPickupDate  = pickupDate,
            RequestedPickupSlot  = pickupSlot,
            PaymentMethod        = paymentMethod,
            SpecialInstructions  = notes,
            Items                = Items.Select(i => i.ToRequest()).ToList()
        };
    }
}
