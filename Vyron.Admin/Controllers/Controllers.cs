using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Vyron.Admin.Data;
using Vyron.Admin.Helpers;
using Vyron.Admin.Models;
using Vyron.Admin.Services;
using Vyron.API.Models;
using Vyron.Shared.Enums;

namespace Vyron.Admin.Controllers;

// ═══════════════════════════════════════════════════════════════════
// ACCOUNT — role-aware login (Admin → /Home, StoreOwner → /StoreOwner)
// ═══════════════════════════════════════════════════════════════════
public class AccountController : Controller
{
    private readonly AdminDbContext _db;
    private readonly IUserRepo _users;
    private readonly IPasswordResetRepo _resetRepo;
    private readonly IWebHostEnvironment _env;

    public AccountController(AdminDbContext db, IUserRepo users, IPasswordResetRepo resetRepo, IWebHostEnvironment env)
    { _db = db; _users = users; _resetRepo = resetRepo; _env = env; }

    [AllowAnonymous]
    public IActionResult Index() => RedirectToAction(nameof(Login));

    [HttpGet, AllowAnonymous]
    public IActionResult Login(string? returnUrl = null, bool expired = false)
    {
        ViewData["ReturnUrl"] = returnUrl;
        ViewData["Expired"] = expired;
        return View();
    }

    [HttpPost, AllowAnonymous, ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(string identifier, string password,
        bool rememberMe = false, string? returnUrl = null)
    {
        // Try DB-backed auth first (by email or phone)
        var user = await _db.Users.FirstOrDefaultAsync(u =>
            (u.Email == identifier || u.Phone == identifier) &&
            (u.Role == UserRole.Admin || u.Role == UserRole.SuperAdmin ||
             u.Role == UserRole.AdminUser || u.Role == UserRole.StoreOwner) &&
            u.IsActive);

        if (user != null)
        {
            bool ok = false;
            if (!string.IsNullOrEmpty(user.PasswordHash))
                ok = FileUploadHelper.VerifyPassword(user, user.PasswordHash, password);
            else // legacy bootstrap credentials
                ok = (user.Role == UserRole.Admin && identifier == "admin" && password == "Vyron@Admin2024!")
                  || (user.Role == UserRole.Admin && password == "Vyron@Admin2024!")
                  || (user.Role == UserRole.StoreOwner && password == "StoreOwner@2024");

            if (ok)
            {
                user.LastLoginAt = DateTime.UtcNow;
                await _db.SaveChangesAsync();
                bool isPortalAdmin = user.Role == UserRole.Admin || user.Role == UserRole.SuperAdmin
                                  || user.Role == UserRole.AdminUser;
                await SignInAsync(user, rememberMe);
                return isPortalAdmin
                    ? RedirectToAction("Index", "Home")
                    : RedirectToAction("Dashboard", "StoreOwner");
            }
        }
        // Legacy hard-coded admin shortcut
        else if (identifier == "admin" && password == "Vyron@Admin2024!")
        {
            var admin = await _db.Users.FirstOrDefaultAsync(u => u.Role == UserRole.Admin);
            if (admin != null)
            {
                admin.LastLoginAt = DateTime.UtcNow;
                await _db.SaveChangesAsync();
                await SignInAsync(admin, rememberMe);
                return RedirectToAction("Index", "Home");
            }
        }

        ModelState.AddModelError(string.Empty, "Invalid credentials.");
        ViewData["ReturnUrl"] = returnUrl;
        return View();
    }

    private async Task SignInAsync(User user, bool rememberMe = false)
    {
        // Emit the exact role name so [Authorize(Roles="SuperAdmin")] etc. work correctly
        var roleClaim = user.Role switch {
            UserRole.SuperAdmin  => "SuperAdmin",
            UserRole.Admin       => "Admin",
            UserRole.AdminUser   => "AdminUser",
            UserRole.StoreOwner  => "StoreOwner",
            _                    => user.Role.ToString()
        };
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.FullName),
            new(ClaimTypes.MobilePhone, user.Phone),
            new(ClaimTypes.Role, roleClaim)
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        // Remember Me: persist cookie for 30 days. Otherwise session cookie (browser-lifetime).
        var expiry = rememberMe
            ? DateTimeOffset.UtcNow.AddDays(30)
            : DateTimeOffset.UtcNow.AddHours(8);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties { IsPersistent = rememberMe, ExpiresUtc = expiry });
    }

    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Login");
    }

    [AllowAnonymous]
    public async Task<IActionResult> LogoutIdle()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Login", new { expired = true });
    }

    [AllowAnonymous] public IActionResult AccessDenied() => View();

    // ─── Profile ───────────────────────────────────────────────────
    [Authorize]
    public async Task<IActionResult> Profile()
    {
        var id = GetCurrentUserId();
        var user = await _users.GetByIdAsync(id);
        if (user == null) return NotFound();
        var vm = new ProfileEditVm
        {
            FullName = user.FullName,
            Email = user.Email,
            CountryCode = user.CountryCode,
            LocalPhone = user.LocalPhone,
            CurrentProfilePhoto = user.ProfilePhoto
        };
        ViewData["Title"] = "My Profile";
        return View(vm);
    }

    [HttpPost, Authorize, ValidateAntiForgeryToken]
    public async Task<IActionResult> Profile(ProfileEditVm vm)
    {
        if (!ModelState.IsValid) { ViewData["Title"] = "My Profile"; return View(vm); }
        var id = GetCurrentUserId();
        var user = await _users.GetByIdAsync(id);
        if (user == null) return NotFound();

        user.FullName = vm.FullName;
        user.Email = vm.Email;
        user.CountryCode = vm.CountryCode ?? "+234";
        user.LocalPhone = vm.LocalPhone;

        if (vm.ProfileImageFile != null)
        {
            if (!FileUploadHelper.IsValidImageFile(vm.ProfileImageFile, out var imgErr))
            { ModelState.AddModelError("ProfileImageFile", imgErr!); ViewData["Title"] = "My Profile"; return View(vm); }
            user.ProfilePhoto = await FileUploadHelper.SaveProfileImageAsync(vm.ProfileImageFile, _env);
            user.ProfileImageFileName = vm.ProfileImageFile.FileName;
            user.ProfileImageContentType = vm.ProfileImageFile.ContentType;
            user.ProfileImageSize = vm.ProfileImageFile.Length;
        }

        await _users.UpdateProfileAsync(user);
        TempData["Success"] = "Profile updated successfully.";
        return RedirectToAction(nameof(Profile));
    }

    // ─── Change Password ───────────────────────────────────────────
    [Authorize]
    public IActionResult ChangePassword()
    {
        ViewData["Title"] = "Change Password";
        return View(new ChangePasswordVm());
    }

    [HttpPost, Authorize, ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(ChangePasswordVm vm)
    {
        if (!ModelState.IsValid) { ViewData["Title"] = "Change Password"; return View(vm); }
        var (ok, err) = await _users.ChangePasswordAsync(GetCurrentUserId(), vm.CurrentPassword, vm.NewPassword);
        if (!ok) { ModelState.AddModelError(string.Empty, err!); ViewData["Title"] = "Change Password"; return View(vm); }
        TempData["Success"] = "Password changed successfully.";
        return RedirectToAction(nameof(Profile));
    }

    // ─── Forgot Password ───────────────────────────────────────────
    [HttpGet, AllowAnonymous]
    public IActionResult ForgotPassword() => View(new ForgotPasswordVm());

    [HttpPost, AllowAnonymous, ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordVm vm)
    {
        if (!ModelState.IsValid) return View(vm);
        var user = await _db.Users.FirstOrDefaultAsync(u =>
            u.Email == vm.Identifier || u.Phone == vm.Identifier);

        // Always show same message to avoid user enumeration
        ViewData["EmailSent"] = true;

        if (user != null && (user.Role == UserRole.Admin || user.Role == UserRole.SuperAdmin ||
                              user.Role == UserRole.AdminUser || user.Role == UserRole.StoreOwner))
        {
            var (raw, _) = await _resetRepo.CreateTokenAsync(user.Id);
            var link = Url.Action("ResetPassword", "Account", new { token = raw }, Request.Scheme);
            ViewData["DevResetLink"] = link; // Only shown in Development
        }
        return View(vm);
    }

    [HttpGet, AllowAnonymous]
    public async Task<IActionResult> ResetPassword(string token)
    {
        if (string.IsNullOrEmpty(token)) return RedirectToAction(nameof(Login));
        var record = await _resetRepo.GetValidByTokenAsync(token);
        if (record == null) { TempData["Error"] = "This reset link is invalid or has expired."; return RedirectToAction(nameof(Login)); }
        return View(new ResetPasswordVm { Token = token });
    }

    [HttpPost, AllowAnonymous, ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(ResetPasswordVm vm)
    {
        if (!ModelState.IsValid) return View(vm);
        var record = await _resetRepo.GetValidByTokenAsync(vm.Token);
        if (record == null) { TempData["Error"] = "This reset link is invalid or has expired."; return RedirectToAction(nameof(Login)); }
        await _resetRepo.ResetPasswordAsync(record.Id, record.UserId, vm.NewPassword);
        TempData["Success"] = "Password reset successfully. You can now log in.";
        return RedirectToAction(nameof(Login));
    }

    private Guid GetCurrentUserId() =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : Guid.Empty;
}

// ═══════════════════════════════════════════════════════════════════
// SHARED — base controller (Guid helpers)
// ═══════════════════════════════════════════════════════════════════
[Authorize]
public abstract class VyronAdminController : Controller
{
    protected Guid CurrentUserId =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : Guid.Empty;
    protected bool IsAdmin => User.IsInRole("Admin") || User.IsInRole("SuperAdmin") || User.IsInRole("AdminUser");
    protected bool IsStoreOwner => User.IsInRole("StoreOwner");
}

// ═══════════════════════════════════════════════════════════════════
// HOME — admin dashboard
// ═══════════════════════════════════════════════════════════════════
[Authorize(Roles = "Admin,SuperAdmin")]
public class HomeController : VyronAdminController
{
    private readonly IAdminAnalytics _analytics;
    public HomeController(IAdminAnalytics analytics) => _analytics = analytics;

    public async Task<IActionResult> Index() => View(await _analytics.GetDashboardAsync());
}

// ═══════════════════════════════════════════════════════════════════
// ORDERS — admin order management
// ═══════════════════════════════════════════════════════════════════
[Authorize(Roles = "Admin,SuperAdmin")]
public class OrdersController : VyronAdminController
{
    private readonly IOrderRepo _orders;
    private readonly IRiderRepo _riders;

    public OrdersController(IOrderRepo orders, IRiderRepo riders)
    { _orders = orders; _riders = riders; }

    public async Task<IActionResult> Index(OrderStatus? status, string? search, int page = 1)
    {
        ViewBag.Status = status; ViewBag.Search = search; ViewBag.Page = page;
        ViewBag.Total = await _orders.GetTotalCountAsync(status, search);
        return View(await _orders.GetAllAsync(status, search, page));
    }

    public async Task<IActionResult> Detail(Guid id)
    {
        var order = await _orders.GetByIdAsync(id);
        if (order == null) return NotFound();
        ViewBag.Riders = await _riders.GetApprovedAsync();
        return View(order);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateStatus(Guid id, OrderStatus newStatus, string? note)
    {
        // PickUpRiderAssigned and DeliveryRiderAssigned must be set via the assignment actions only.
        // Manual dropdown selection of these statuses is SuperAdmin-only.
        if (newStatus is OrderStatus.PickUpRiderAssigned or OrderStatus.DeliveryRiderAssigned)
        {
            if (!User.IsInRole("SuperAdmin"))
            {
                TempData["Error"] = "Only SuperAdmin can manually set PickUpRiderAssigned or DeliveryRiderAssigned status. Use the rider assignment actions instead.";
                return RedirectToAction("Detail", new { id });
            }
        }
        await _orders.UpdateStatusAsync(id, newStatus, note, CurrentUserId);
        TempData["Success"] = $"Status updated to {newStatus}.";
        return RedirectToAction("Detail", new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AssignRider(Guid id, Guid riderId)
    {
        await _orders.AssignRiderAsync(id, riderId, CurrentUserId);
        TempData["Success"] = "Rider assigned successfully.";
        return RedirectToAction("Detail", new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> OverridePrice(Guid id, decimal actualLaundryCost, string reason)
    {
        await _orders.OverridePriceAsync(id, actualLaundryCost, reason, CurrentUserId);
        TempData["Success"] = "Price overridden.";
        return RedirectToAction("Detail", new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AssignDeliveryRider(Guid id, Guid deliveryRiderId)
    {
        await _orders.AssignDeliveryRiderAsync(id, deliveryRiderId, CurrentUserId);
        TempData["Success"] = "Delivery rider assigned.";
        return RedirectToAction("Detail", new { id });
    }
}

// ═══════════════════════════════════════════════════════════════════
// STORES — admin
// ═══════════════════════════════════════════════════════════════════
[Authorize(Roles = "Admin,SuperAdmin")]
public class StoresController : VyronAdminController
{
    private readonly IStoreRepo _stores;
    private readonly IStoreExtendedRepo _storeExt;
    private readonly IStoreImageRepo _images;
    private readonly IWebHostEnvironment _env;

    public StoresController(IStoreRepo stores, IStoreExtendedRepo storeExt, IStoreImageRepo images, IWebHostEnvironment env)
    { _stores = stores; _storeExt = storeExt; _images = images; _env = env; }

    public async Task<IActionResult> Index() => View(await _stores.GetAllAsync());

    public async Task<IActionResult> Detail(Guid id)
    {
        var store = await _stores.GetByIdAsync(id);
        if (store == null) return NotFound();
        ViewBag.Images = await _images.GetByStoreAsync(id);
        return View(store);
    }

    // ─── Create ────────────────────────────────────────────────────
    public async Task<IActionResult> Create()
    {
        ViewBag.Owners = await _storeExt.GetStoreOwnersAsync();
        return View(new StoreCreateVm());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(StoreCreateVm vm)
    {
        if (!ModelState.IsValid) { ViewBag.Owners = await _storeExt.GetStoreOwnersAsync(); return View(vm); }
        if (vm.OwnerId == null || vm.OwnerId == Guid.Empty)
        { ModelState.AddModelError("OwnerId", "Please select a store owner."); ViewBag.Owners = await _storeExt.GetStoreOwnersAsync(); return View(vm); }

        var store = new LaundryStore
        {
            OwnerId = vm.OwnerId.Value, Name = vm.Name, Description = vm.Description ?? string.Empty,
            Phone = vm.Phone, Email = vm.Email ?? string.Empty, Address = vm.Address, Area = vm.Area,
            City = vm.City, State = vm.State, PickupFee = vm.PickupFee, DeliveryFee = vm.DeliveryFee,
            OpeningHours = vm.OpeningHours, Status = StoreStatus.Active
        };
        var storeId = await _storeExt.CreateAsync(store);

        if (vm.ImageFile != null)
        {
            if (!FileUploadHelper.IsValidImageFile(vm.ImageFile, out var imgErr))
            { TempData["Error"] = $"Store created but image skipped: {imgErr}"; return RedirectToAction("Detail", new { id = storeId }); }
            var path = await FileUploadHelper.SaveStoreImageAsync(vm.ImageFile, _env);
            await _images.AddAsync(new StoreImage { StoreId = storeId, ImagePath = path, FileName = vm.ImageFile.FileName, ContentType = vm.ImageFile.ContentType, FileSize = vm.ImageFile.Length, UploadedByUserId = CurrentUserId });
        }

        TempData["Success"] = "Store created successfully.";
        return RedirectToAction("Detail", new { id = storeId });
    }

    // ─── Edit ──────────────────────────────────────────────────────
    public async Task<IActionResult> Edit(Guid id)
    {
        var store = await _stores.GetByIdAsync(id);
        if (store == null) return NotFound();
        var vm = new StoreEditVm { Id = store.Id, Name = store.Name, Description = store.Description, Phone = store.Phone, Email = store.Email, Address = store.Address, Area = store.Area, City = store.City, State = store.State, PickupFee = store.PickupFee, DeliveryFee = store.DeliveryFee, OpeningHours = store.OpeningHours, CurrentLogoUrl = store.LogoUrl };
        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(StoreEditVm vm)
    {
        if (!ModelState.IsValid) return View(vm);
        var store = await _stores.GetByIdAsync(vm.Id);
        if (store == null) return NotFound();
        store.Name = vm.Name; store.Description = vm.Description ?? string.Empty;
        store.Phone = vm.Phone; store.Email = vm.Email ?? string.Empty;
        store.Address = vm.Address; store.Area = vm.Area;
        store.City = vm.City; store.State = vm.State;
        store.PickupFee = vm.PickupFee; store.DeliveryFee = vm.DeliveryFee;
        store.OpeningHours = vm.OpeningHours;
        await _stores.UpdateProfileAsync(store);

        if (vm.ImageFile != null)
        {
            if (!FileUploadHelper.IsValidImageFile(vm.ImageFile, out var imgErr))
            { TempData["Error"] = $"Store saved but image skipped: {imgErr}"; return RedirectToAction("Detail", new { id = vm.Id }); }
            var path = await FileUploadHelper.SaveStoreImageAsync(vm.ImageFile, _env);
            await _images.AddAsync(new StoreImage { StoreId = vm.Id, ImagePath = path, FileName = vm.ImageFile.FileName, ContentType = vm.ImageFile.ContentType, FileSize = vm.ImageFile.Length, UploadedByUserId = CurrentUserId });
        }

        TempData["Success"] = "Store updated.";
        return RedirectToAction("Detail", new { id = vm.Id });
    }

    // ─── Status actions ────────────────────────────────────────────
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(Guid id)
    {
        var (ok, err) = await _storeExt.ApproveAsync(id, CurrentUserId);
        if (ok) TempData["Success"] = "Store approved and set to Active.";
        else TempData["Error"] = err;
        return RedirectToAction("Detail", new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateStatus(Guid id, StoreStatus status)
    {
        await _stores.UpdateStatusAsync(id, status);
        TempData["Success"] = $"Store status updated to {status}.";
        return RedirectToAction("Detail", new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleVerified(Guid id)
    {
        await _stores.ToggleVerifiedAsync(id);
        return RedirectToAction("Detail", new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleTopRated(Guid id)
    {
        await _stores.ToggleTopRatedAsync(id);
        return RedirectToAction("Detail", new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleStoreOpen(Guid id)
    {
        var store = await _stores.GetByIdAsync(id);
        if (store == null) return NotFound();
        await _stores.ToggleManualCloseAsync(id, !store.IsManuallyClosed);
        TempData["Success"] = store.IsManuallyClosed ? "Store is now open." : "Store is now closed.";
        return RedirectToAction("Detail", new { id });
    }

    // ─── Store images ──────────────────────────────────────────────
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadImage(Guid storeId, IFormFile file)
    {
        if (!FileUploadHelper.IsValidImageFile(file, out var err))
        { TempData["ImageError"] = err; return RedirectToAction("Detail", new { id = storeId }); }
        var path = await FileUploadHelper.SaveStoreImageAsync(file, _env);
        await _images.AddAsync(new StoreImage { StoreId = storeId, ImagePath = path, FileName = file.FileName, ContentType = file.ContentType, FileSize = file.Length, UploadedByUserId = CurrentUserId });
        TempData["ImageSuccess"] = "Image uploaded.";
        return RedirectToAction("Detail", new { id = storeId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteImage(Guid storeId, Guid imageId)
    {
        var img = (await _images.GetByStoreAsync(storeId)).FirstOrDefault(i => i.Id == imageId);
        var (ok, err) = await _images.DeleteAsync(imageId, storeId);
        if (ok) { FileUploadHelper.DeleteFile(img?.ImagePath, _env); TempData["ImageSuccess"] = "Image deleted."; }
        else TempData["ImageError"] = err;
        return RedirectToAction("Detail", new { id = storeId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SetPrimaryImage(Guid storeId, Guid imageId)
    {
        await _images.SetPrimaryAsync(imageId, storeId);
        TempData["ImageSuccess"] = "Primary image updated.";
        return RedirectToAction("Detail", new { id = storeId });
    }
}

// ═══════════════════════════════════════════════════════════════════
// RIDERS
// ═══════════════════════════════════════════════════════════════════
[Authorize(Roles = "Admin,SuperAdmin")]
public class RidersController : VyronAdminController
{
    private readonly IRiderRepo _riders;
    private readonly IRiderExtendedRepo _riderExt;
    private readonly IWebHostEnvironment _env;

    public RidersController(IRiderRepo riders, IRiderExtendedRepo riderExt, IWebHostEnvironment env)
    { _riders = riders; _riderExt = riderExt; _env = env; }

    public async Task<IActionResult> Index() => View(await _riders.GetAllAsync());

    public async Task<IActionResult> Details(Guid id)
    {
        var rider = await _riders.GetByIdAsync(id);
        return rider == null ? NotFound() : View(rider);
    }

    public IActionResult Create() => View(new RiderCreateVm());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(RiderCreateVm vm)
    {
        if (!ModelState.IsValid) return View(vm);
        var phone = NormalizePhone(vm.CountryCode, vm.LocalPhone);
        string? profilePath = null;
        if (vm.ProfileImageFile != null)
        {
            if (!FileUploadHelper.IsValidImageFile(vm.ProfileImageFile, out var imgErr))
            { ModelState.AddModelError("ProfileImageFile", imgErr!); return View(vm); }
            profilePath = await FileUploadHelper.SaveProfileImageAsync(vm.ProfileImageFile, _env);
        }
        var ok = await _riders.CreateAsync(vm.FullName, phone, vm.VehicleType, vm.VehiclePlate, profilePath, CurrentUserId);
        if (ok) { TempData["Success"] = "Rider registered and pending approval."; return RedirectToAction("Index"); }
        ModelState.AddModelError("", "A user with this phone already exists.");
        return View(vm);
    }

    public async Task<IActionResult> Edit(Guid id)
    {
        var rider = await _riders.GetByIdAsync(id);
        if (rider == null) return NotFound();
        var vm = new RiderEditVm { Id = rider.Id, FullName = rider.User.FullName, VehicleType = rider.VehicleType, VehiclePlate = rider.VehiclePlate, Status = rider.Status, CurrentProfilePhoto = rider.User.ProfilePhoto };
        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(RiderEditVm vm)
    {
        if (!ModelState.IsValid) return View(vm);
        var rider = await _riders.GetByIdAsync(vm.Id);
        if (rider == null) return NotFound();

        string? profilePath = null;
        string? fileName = null; string? contentType = null; long fileSize = 0;
        if (vm.ProfileImageFile != null)
        {
            if (!FileUploadHelper.IsValidImageFile(vm.ProfileImageFile, out var imgErr))
            { ModelState.AddModelError("ProfileImageFile", imgErr!); return View(vm); }
            profilePath = await FileUploadHelper.SaveProfileImageAsync(vm.ProfileImageFile, _env);
            fileName = vm.ProfileImageFile.FileName; contentType = vm.ProfileImageFile.ContentType; fileSize = vm.ProfileImageFile.Length;
        }

        var updatedRider = new Rider { Id = rider.Id, UserId = rider.UserId, VehicleType = vm.VehicleType, VehiclePlate = vm.VehiclePlate };
        var updatedUser = new User { Id = rider.UserId, FullName = vm.FullName, ProfilePhoto = profilePath, ProfileImageFileName = fileName, ProfileImageContentType = contentType, ProfileImageSize = fileSize };
        await _riderExt.UpdateAsync(updatedRider, updatedUser);
        if (vm.Status != rider.Status) await _riderExt.UpdateStatusAsync(rider.Id, vm.Status);
        TempData["Success"] = "Rider updated.";
        return RedirectToAction("Details", new { id = rider.Id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(Guid id)
    {
        var (ok, err) = await _riderExt.ApproveAsync(id, CurrentUserId);
        if (ok) TempData["Success"] = "Rider approved successfully.";
        else TempData["Error"] = err;
        return RedirectToAction("Details", new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(Guid id, string reason)
    {
        var (ok, err) = await _riderExt.RejectAsync(id, CurrentUserId, reason);
        if (ok) TempData["Success"] = "Rider rejected.";
        else TempData["Error"] = err;
        return RedirectToAction("Details", new { id });
    }

    public async Task<IActionResult> Pending()
        => View(await _riders.GetPendingApprovalAsync());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleOnline(Guid id)
    {
        await _riders.ToggleOnlineAsync(id);
        return RedirectToAction("Index");
    }

    private static string NormalizePhone(string countryCode, string local)
    {
        local = local.TrimStart('0').Trim();
        return countryCode.Trim() + local;
    }
}

// ═══════════════════════════════════════════════════════════════════
// DISPUTES
// ═══════════════════════════════════════════════════════════════════
[Authorize(Roles = "Admin,SuperAdmin")]
public class DisputesController : VyronAdminController
{
    private readonly IDisputeRepo _disputes;
    public DisputesController(IDisputeRepo disputes) => _disputes = disputes;

    public async Task<IActionResult> Index(DisputeStatus? status)
    {
        ViewBag.Status = status;
        return View(await _disputes.GetAllAsync(status));
    }

    public async Task<IActionResult> Detail(Guid id)
    {
        var d = await _disputes.GetByIdAsync(id);
        return d == null ? NotFound() : View(d);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateStatus(Guid id, DisputeStatus status, string? note)
    {
        await _disputes.UpdateStatusAsync(id, status, note);
        TempData["Success"] = $"Status updated to {status}.";
        return RedirectToAction("Detail", new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddMessage(Guid id, string message)
    {
        await _disputes.AddMessageAsync(id, CurrentUserId, message, isAdmin: true);
        return RedirectToAction("Detail", new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Resolve(Guid id, DisputeResolution resolution, string note, decimal? refundAmount)
    {
        await _disputes.ResolveAsync(id, resolution, note, refundAmount);
        TempData["Success"] = "Dispute resolved.";
        return RedirectToAction("Detail", new { id });
    }
}

// ═══════════════════════════════════════════════════════════════════
// REVIEWS
// ═══════════════════════════════════════════════════════════════════
[Authorize(Roles = "Admin,SuperAdmin")]
public class ReviewsController : VyronAdminController
{
    private readonly IReviewRepo _reviews;
    public ReviewsController(IReviewRepo reviews) => _reviews = reviews;

    public async Task<IActionResult> Index() => View(await _reviews.GetAllAsync());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Moderate(Guid id, bool visible, string? note)
    {
        await _reviews.ModerateAsync(id, visible, note);
        TempData["Success"] = visible ? "Review approved." : "Review hidden.";
        return RedirectToAction("Index");
    }
}

// ═══════════════════════════════════════════════════════════════════
// PAYMENTS
// ═══════════════════════════════════════════════════════════════════
[Authorize(Roles = "Admin,SuperAdmin")]
public class PaymentsController : VyronAdminController
{
    private readonly IPaymentRepo _payments;
    public PaymentsController(IPaymentRepo payments) => _payments = payments;

    public async Task<IActionResult> Index(int page = 1)
    {
        ViewBag.Page = page;
        return View(await _payments.GetAllAsync(page));
    }
}

// ═══════════════════════════════════════════════════════════════════
// SETTINGS
// ═══════════════════════════════════════════════════════════════════
[Authorize(Roles = "Admin,SuperAdmin")]
public class SettingsController : VyronAdminController
{
    private readonly IConfigRepo _config;
    public SettingsController(IConfigRepo config) => _config = config;

    public async Task<IActionResult> Index() => View(await _config.GetAllAsync());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(string key, string value)
    {
        await _config.UpdateAsync(key, value, CurrentUserId);
        TempData["Success"] = $"{key} updated.";
        return RedirectToAction("Index");
    }
}

// ═══════════════════════════════════════════════════════════════════
// STORE OWNER PORTAL — restricted to owners' own stores
// ═══════════════════════════════════════════════════════════════════
[Authorize(Roles = "StoreOwner,Admin,SuperAdmin")]
public class StoreOwnerController : VyronAdminController
{
    private readonly IAdminAnalytics _analytics;
    private readonly IOrderRepo _orders;
    private readonly IStoreRepo _stores;
    private readonly IDisputeRepo _disputes;
    private readonly IReviewRepo _reviews;
    private readonly AdminDbContext _db;

    public StoreOwnerController(IAdminAnalytics analytics, IOrderRepo orders,
        IStoreRepo stores, IDisputeRepo disputes, IReviewRepo reviews, AdminDbContext db)
    {
        _analytics = analytics; _orders = orders; _stores = stores;
        _disputes = disputes; _reviews = reviews; _db = db;
    }

    public async Task<IActionResult> Dashboard()
    {
        var vm = await _analytics.GetOwnerDashboardAsync(CurrentUserId);
        return View(vm);
    }

    public async Task<IActionResult> Orders(OrderStatus? status, int page = 1)
    {
        ViewBag.Status = status; ViewBag.Page = page;
        return View(await _orders.GetByOwnerAsync(CurrentUserId, status, page));
    }

    public async Task<IActionResult> OrderDetail(Guid id)
    {
        var order = await _orders.GetByIdAsync(id);
        if (order == null) return NotFound();
        // Verify ownership
        if (!IsAdmin && order.Store.OwnerId != CurrentUserId) return Forbid();
        return View(order);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AcceptOrder(Guid id)
    {
        var order = await _db.Orders.Include(o => o.Store).FirstOrDefaultAsync(o => o.Id == id);
        if (order == null) return NotFound();
        if (!IsAdmin && order.Store.OwnerId != CurrentUserId) return Forbid();
        await _orders.UpdateStatusAsync(id, OrderStatus.Confirmed,
            "Accepted by store", CurrentUserId);
        TempData["Success"] = $"Order {order.OrderNumber} accepted.";
        return RedirectToAction("OrderDetail", new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> StartProcessing(Guid id)
    {
        var order = await _db.Orders.Include(o => o.Store).FirstOrDefaultAsync(o => o.Id == id);
        if (order == null) return NotFound();
        if (!IsAdmin && order.Store.OwnerId != CurrentUserId) return Forbid();
        await _orders.UpdateStatusAsync(id, OrderStatus.Processing,
            "Laundry being processed", CurrentUserId);
        TempData["Success"] = "Order marked as processing.";
        return RedirectToAction("OrderDetail", new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkReady(Guid id)
    {
        var order = await _db.Orders.Include(o => o.Store).FirstOrDefaultAsync(o => o.Id == id);
        if (order == null) return NotFound();
        if (!IsAdmin && order.Store.OwnerId != CurrentUserId) return Forbid();
        await _orders.UpdateStatusAsync(id, OrderStatus.Ready,
            "Laundry ready for delivery", CurrentUserId);
        TempData["Success"] = "Order marked as ready.";
        return RedirectToAction("OrderDetail", new { id });
    }

    public async Task<IActionResult> Stores()
        => View(await _stores.GetByOwnerAsync(CurrentUserId));

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleStoreOpen(Guid id)
    {
        var store = await _stores.GetByIdAsync(id);
        if (store == null) return NotFound();
        if (!IsAdmin && store.OwnerId != CurrentUserId) return Forbid();
        await _stores.ToggleManualCloseAsync(id, !store.IsManuallyClosed);
        TempData["Success"] = store.IsManuallyClosed ? "Store is now open." : "Store is now closed.";
        return RedirectToAction("Stores");
    }

    public async Task<IActionResult> StoreEdit(Guid id, [FromServices] IStoreImageRepo images)
    {
        var s = await _stores.GetByIdAsync(id);
        if (s == null) return NotFound();
        if (!IsAdmin && s.OwnerId != CurrentUserId) return Forbid();
        ViewBag.Images = await images.GetByStoreAsync(id);
        return View(s);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> StoreEdit(LaundryStore updated, [FromServices] IStoreImageRepo images)
    {
        var s = await _stores.GetByIdAsync(updated.Id);
        if (s == null) return NotFound();
        if (!IsAdmin && s.OwnerId != CurrentUserId) return Forbid();
        await _stores.UpdateProfileAsync(updated);
        TempData["Success"] = "Store updated.";
        return RedirectToAction("Stores");
    }

    // ─── Store Owner Image Upload ──────────────────────────────────
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadStoreImage(Guid storeId, IFormFile imageFile,
        [FromServices] IStoreImageRepo images, [FromServices] IWebHostEnvironment env)
    {
        var store = await _stores.GetByIdAsync(storeId);
        if (store == null) return NotFound();
        if (!IsAdmin && store.OwnerId != CurrentUserId) return Forbid();

        if (!FileUploadHelper.IsValidImageFile(imageFile, out var err))
        { TempData["ImageError"] = err; return RedirectToAction("StoreEdit", new { id = storeId }); }

        var path = await FileUploadHelper.SaveStoreImageAsync(imageFile, env);
        await images.AddAsync(new StoreImage
        {
            StoreId = storeId, ImagePath = path, FileName = imageFile.FileName,
            ContentType = imageFile.ContentType, FileSize = imageFile.Length, UploadedByUserId = CurrentUserId
        });
        TempData["ImageSuccess"] = "Image uploaded successfully.";
        return RedirectToAction("StoreEdit", new { id = storeId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteStoreImage(Guid storeId, Guid imageId,
        [FromServices] IStoreImageRepo images, [FromServices] IWebHostEnvironment env)
    {
        var store = await _stores.GetByIdAsync(storeId);
        if (store == null) return NotFound();
        if (!IsAdmin && store.OwnerId != CurrentUserId) return Forbid();

        var img = (await images.GetByStoreAsync(storeId)).FirstOrDefault(i => i.Id == imageId);
        var (ok, err) = await images.DeleteAsync(imageId, storeId);
        if (ok) { FileUploadHelper.DeleteFile(img?.ImagePath, env); TempData["ImageSuccess"] = "Image deleted."; }
        else TempData["ImageError"] = err;
        return RedirectToAction("StoreEdit", new { id = storeId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SetPrimaryStoreImage(Guid storeId, Guid imageId,
        [FromServices] IStoreImageRepo images)
    {
        var store = await _stores.GetByIdAsync(storeId);
        if (store == null) return NotFound();
        if (!IsAdmin && store.OwnerId != CurrentUserId) return Forbid();

        await images.SetPrimaryAsync(imageId, storeId);
        TempData["ImageSuccess"] = "Primary image updated.";
        return RedirectToAction("StoreEdit", new { id = storeId });
    }

    public async Task<IActionResult> Disputes()
        => View(await _disputes.GetByOwnerStoresAsync(CurrentUserId));

    public async Task<IActionResult> DisputeDetail(Guid id)
    {
        var d = await _disputes.GetByIdAsync(id);
        if (d == null) return NotFound();
        if (!IsAdmin && d.Order.Store.OwnerId != CurrentUserId) return Forbid();
        return View(d);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ReplyDispute(Guid id, string message)
    {
        var d = await _disputes.GetByIdAsync(id);
        if (d == null) return NotFound();
        if (!IsAdmin && d.Order.Store.OwnerId != CurrentUserId) return Forbid();
        await _disputes.AddMessageAsync(id, CurrentUserId, message, isAdmin: false);
        return RedirectToAction("DisputeDetail", new { id });
    }

    public async Task<IActionResult> Reviews()
        => View(await _reviews.GetByOwnerStoresAsync(CurrentUserId));

    // ─── Store Create (StoreOwner requests a new store) ────────────
    public IActionResult StoreCreate() => View(new StoreCreateVm());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> StoreCreate(StoreCreateVm vm, [FromServices] IStoreExtendedRepo storeExt,
        [FromServices] IStoreImageRepo images, [FromServices] IWebHostEnvironment env)
    {
        if (!ModelState.IsValid) return View(vm);
        var store = new LaundryStore
        {
            OwnerId = CurrentUserId, Name = vm.Name, Description = vm.Description ?? string.Empty,
            Phone = vm.Phone, Email = vm.Email ?? string.Empty, Address = vm.Address, Area = vm.Area,
            City = vm.City, State = vm.State, PickupFee = vm.PickupFee, DeliveryFee = vm.DeliveryFee,
            OpeningHours = vm.OpeningHours, Status = StoreStatus.Pending
        };
        var storeId = await storeExt.CreateAsync(store);
        if (vm.ImageFile != null)
        {
            if (!FileUploadHelper.IsValidImageFile(vm.ImageFile, out var imgErr))
            { TempData["Error"] = $"Store submitted but image skipped: {imgErr}"; return RedirectToAction("Stores"); }
            var path = await FileUploadHelper.SaveStoreImageAsync(vm.ImageFile, env);
            await images.AddAsync(new StoreImage { StoreId = storeId, ImagePath = path, FileName = vm.ImageFile.FileName, ContentType = vm.ImageFile.ContentType, FileSize = vm.ImageFile.Length, UploadedByUserId = CurrentUserId });
        }
        TempData["Success"] = "Store submitted for Admin approval.";
        return RedirectToAction("Stores");
    }

    // ─── Service Offerings (StoreOwner) ───────────────────────────
    public async Task<IActionResult> ServiceOfferings([FromServices] IServiceOfferingRepo svcRepo)
    {
        var storeIds = (await _stores.GetByOwnerAsync(CurrentUserId)).Select(s => s.Id).ToList();
        var all = await svcRepo.GetAllAsync();
        return View(all.Where(s => storeIds.Contains(s.StoreId)).ToList());
    }

    public async Task<IActionResult> ServiceCreate([FromServices] IServiceOfferingRepo svcRepo)
    {
        var stores = await _stores.GetByOwnerAsync(CurrentUserId);
        ViewBag.Stores = stores;
        return View(new ServiceOfferingCreateVm());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ServiceCreate(ServiceOfferingCreateVm vm, [FromServices] IServiceOfferingRepo svcRepo)
    {
        if (!ModelState.IsValid) { ViewBag.Stores = await _stores.GetByOwnerAsync(CurrentUserId); return View(vm); }
        var myStores = await _stores.GetByOwnerAsync(CurrentUserId);
        if (!myStores.Any(s => s.Id == vm.StoreId))
        { ModelState.AddModelError("StoreId", "Invalid store selection."); ViewBag.Stores = myStores; return View(vm); }
        await svcRepo.CreateAsync(new ServiceOffering { StoreId = vm.StoreId, Name = vm.Name, Description = vm.Description, Category = vm.Category, ServiceType = vm.ServiceType, PricingMode = vm.PricingMode, BasePrice = vm.BasePrice, MinimumCharge = vm.MinimumCharge, EstimatedHours = vm.EstimatedHours, IsActive = vm.IsActive });
        TempData["Success"] = "Service offering created.";
        return RedirectToAction("ServiceOfferings");
    }

    public async Task<IActionResult> ServiceEdit(Guid id, [FromServices] IServiceOfferingRepo svcRepo)
    {
        var s = await svcRepo.GetByIdAsync(id);
        if (s == null) return NotFound();
        var myStores = await _stores.GetByOwnerAsync(CurrentUserId);
        if (!IsAdmin && !myStores.Any(st => st.Id == s.StoreId)) return Forbid();
        var vm = new ServiceOfferingEditVm { Id = s.Id, StoreId = s.StoreId, Name = s.Name, Description = s.Description, Category = s.Category, ServiceType = s.ServiceType, PricingMode = s.PricingMode, BasePrice = s.BasePrice, MinimumCharge = s.MinimumCharge, EstimatedHours = s.EstimatedHours, IsActive = s.IsActive };
        ViewBag.Stores = myStores;
        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ServiceEdit(ServiceOfferingEditVm vm, [FromServices] IServiceOfferingRepo svcRepo)
    {
        if (!ModelState.IsValid) { ViewBag.Stores = await _stores.GetByOwnerAsync(CurrentUserId); return View(vm); }
        var existing = await svcRepo.GetByIdAsync(vm.Id);
        if (existing == null) return NotFound();
        var myStores = await _stores.GetByOwnerAsync(CurrentUserId);
        if (!IsAdmin && !myStores.Any(s => s.Id == existing.StoreId)) return Forbid();
        await svcRepo.UpdateAsync(new ServiceOffering { Id = vm.Id, StoreId = existing.StoreId, Name = vm.Name, Description = vm.Description, Category = vm.Category, ServiceType = vm.ServiceType, PricingMode = vm.PricingMode, BasePrice = vm.BasePrice, MinimumCharge = vm.MinimumCharge, EstimatedHours = vm.EstimatedHours, IsActive = vm.IsActive });
        TempData["Success"] = "Service offering updated.";
        return RedirectToAction("ServiceOfferings");
    }
}

// ═══════════════════════════════════════════════════════════════════
// SERVICES — Admin service offering management
// ═══════════════════════════════════════════════════════════════════
[Authorize(Roles = "Admin,SuperAdmin")]
public class ServicesController : VyronAdminController
{
    private readonly IServiceOfferingRepo _svc;
    private readonly IStoreRepo _stores;
    private readonly AdminDbContext _db;
    public ServicesController(IServiceOfferingRepo svc, IStoreRepo stores, AdminDbContext db) { _svc = svc; _stores = stores; _db = db; }

    private async Task PopulateViewBagsAsync(Guid? selectedStoreId = null)
    {
        ViewBag.Stores      = await _stores.GetAllAsync();
        ViewBag.ServiceTypes = await _db.ServiceTypes
            .Where(t => t.IsActive).OrderBy(t => t.SortOrder).ThenBy(t => t.Name).ToListAsync();
    }

    public async Task<IActionResult> Index(Guid? storeId)
    {
        ViewBag.Stores  = await _stores.GetAllAsync();
        ViewBag.StoreId = storeId;
        return View(await _svc.GetAllAsync(storeId));
    }

    public async Task<IActionResult> Create(Guid? storeId)
    {
        await PopulateViewBagsAsync();
        return View(new ServiceOfferingCreateVm { StoreId = storeId ?? Guid.Empty });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ServiceOfferingCreateVm vm)
    {
        if (!ModelState.IsValid) { await PopulateViewBagsAsync(); return View(vm); }
        await _svc.CreateAsync(new ServiceOffering { StoreId = vm.StoreId, Name = vm.Name, Description = vm.Description, Category = vm.Category, ServiceType = vm.ServiceType, ServiceTypeEntityId = vm.ServiceTypeEntityId, PricingMode = vm.PricingMode, BasePrice = vm.BasePrice, MinimumCharge = vm.MinimumCharge, EstimatedHours = vm.EstimatedHours, IsActive = vm.IsActive });
        TempData["Success"] = "Service offering created.";
        return RedirectToAction("Index");
    }

    public async Task<IActionResult> Edit(Guid id)
    {
        var s = await _svc.GetByIdAsync(id);
        if (s == null) return NotFound();
        await PopulateViewBagsAsync();
        var vm = new ServiceOfferingEditVm { Id = s.Id, StoreId = s.StoreId, Name = s.Name, Description = s.Description, Category = s.Category, ServiceType = s.ServiceType, ServiceTypeEntityId = s.ServiceTypeEntityId, PricingMode = s.PricingMode, BasePrice = s.BasePrice, MinimumCharge = s.MinimumCharge, EstimatedHours = s.EstimatedHours, IsActive = s.IsActive };
        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(ServiceOfferingEditVm vm)
    {
        if (!ModelState.IsValid) { await PopulateViewBagsAsync(); return View(vm); }
        await _svc.UpdateAsync(new ServiceOffering { Id = vm.Id, StoreId = vm.StoreId, Name = vm.Name, Description = vm.Description, Category = vm.Category, ServiceType = vm.ServiceType, ServiceTypeEntityId = vm.ServiceTypeEntityId, PricingMode = vm.PricingMode, BasePrice = vm.BasePrice, MinimumCharge = vm.MinimumCharge, EstimatedHours = vm.EstimatedHours, IsActive = vm.IsActive });
        TempData["Success"] = "Service offering updated.";
        return RedirectToAction("Index");
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleActive(Guid id)
    {
        await _svc.ToggleActiveAsync(id);
        return RedirectToAction("Index");
    }
}

// ═══════════════════════════════════════════════════════════════════
// USERS — Admin-only user management (view, search, activate/deactivate)
// ═══════════════════════════════════════════════════════════════════
[Authorize(Roles = "Admin,SuperAdmin")]
public class UsersController : Controller
{
    private readonly AdminDbContext _db;

    public UsersController(AdminDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> Index(string? search, string? roleFilter, int page = 1)
    {
        const int pageSize = 20;

        var query = _db.Users
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(u =>
                u.FullName.Contains(search) ||
                u.Phone.Contains(search) ||
                (u.Email != null && u.Email.Contains(search)));

        if (!string.IsNullOrWhiteSpace(roleFilter))
            query = query.Where(u =>
                u.UserRoles.Any(ur => ur.Role.NormalizedName == roleFilter.ToUpperInvariant()));

        var total = await query.CountAsync();

        var users = await query
            .OrderByDescending(u => u.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var vm = new UsersFilterVm
        {
            Search     = search,
            RoleFilter = roleFilter,
            Page       = page,
            PageSize   = pageSize,
            TotalCount = total,
            Users      = users.Select(u => new UserListItemVm
            {
                Id          = u.Id,
                FullName    = u.FullName,
                Phone       = u.Phone,
                Email       = u.Email,
                Roles       = string.Join(", ", u.UserRoles.Select(ur => ur.Role?.Name ?? "?")),
                IsActive    = u.IsActive,
                CreatedAt   = u.CreatedAt,
                LastLoginAt = u.LastLoginAt,
                ProfilePhoto = u.ProfilePhoto
            }).ToList()
        };

        ViewData["Title"] = "Users";
        return View(vm);
    }

    [HttpGet]
    public async Task<IActionResult> Details(Guid id)
    {
        var user = await _db.Users
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .Include(u => u.Orders)
            .Include(u => u.Reviews)
            .Include(u => u.Disputes)
            .FirstOrDefaultAsync(u => u.Id == id);

        if (user == null) return NotFound();

        var vm = new UserDetailVm
        {
            Id           = user.Id,
            FullName     = user.FullName,
            Phone        = user.Phone,
            Email        = user.Email,
            Roles        = string.Join(", ", user.UserRoles.Select(ur => ur.Role?.Name ?? "?")),
            RoleList     = user.UserRoles.Select(ur => ur.Role?.Name ?? "?").ToList(),
            IsActive     = user.IsActive,
            CreatedAt    = user.CreatedAt,
            LastLoginAt  = user.LastLoginAt,
            ProfilePhoto = user.ProfilePhoto,
            CountryCode  = user.CountryCode,
            LocalPhone   = user.LocalPhone,
            TotalOrders  = user.Orders.Count,
            TotalReviews = user.Reviews.Count,
            TotalDisputes = user.Disputes.Count
        };

        ViewData["Title"] = $"User: {user.FullName}";
        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleActive(Guid id)
    {
        var user = await _db.Users.FindAsync(id);
        if (user == null) return NotFound();

        user.IsActive = !user.IsActive;
        await _db.SaveChangesAsync();

        TempData["Success"] = user.IsActive
            ? $"{user.FullName} has been activated."
            : $"{user.FullName} has been deactivated.";

        return RedirectToAction(nameof(Index));
    }
}

// ═══════════════════════════════════════════════════════════════════
// SERVICE TYPES — admin-managed service type catalog
// ═══════════════════════════════════════════════════════════════════
[Authorize(Policy = "AdminOnly")]
public class ServiceTypesController : Controller
{
    private readonly AdminDbContext _db;
    public ServiceTypesController(AdminDbContext db) => _db = db;

    public async Task<IActionResult> Index()
    {
        var types = await _db.ServiceTypes.OrderBy(t => t.SortOrder).ThenBy(t => t.Name).ToListAsync();
        return View(types);
    }

    [HttpGet]
    public IActionResult Create() => View(new ServiceTypeEntity());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ServiceTypeEntity model)
    {
        if (!ModelState.IsValid) return View(model);
        model.Id        = Guid.NewGuid();
        model.CreatedAt = DateTime.UtcNow;
        model.UpdatedAt = DateTime.UtcNow;
        _db.ServiceTypes.Add(model);
        await _db.SaveChangesAsync();
        TempData["Success"] = $"Service type '{model.Name}' created.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(Guid id)
    {
        var st = await _db.ServiceTypes.FindAsync(id);
        if (st == null) return NotFound();
        return View(st);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, ServiceTypeEntity model)
    {
        if (!ModelState.IsValid) return View(model);
        var st = await _db.ServiceTypes.FindAsync(id);
        if (st == null) return NotFound();
        st.Name        = model.Name;
        st.Description = model.Description;
        st.SortOrder   = model.SortOrder;
        st.IsActive    = model.IsActive;
        st.UpdatedAt   = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        TempData["Success"] = $"Service type '{st.Name}' updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleActive(Guid id)
    {
        var st = await _db.ServiceTypes.FindAsync(id);
        if (st == null) return NotFound();
        st.IsActive  = !st.IsActive;
        st.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }
}

// ═══════════════════════════════════════════════════════════════════
// COMMUNICATION LOGS — outbound message log
// ═══════════════════════════════════════════════════════════════════
[Authorize(Policy = "AdminOnly")]
public class CommunicationLogsController : Controller
{
    private readonly AdminDbContext _db;
    public CommunicationLogsController(AdminDbContext db) => _db = db;

    public async Task<IActionResult> Index(
        string? channel = null, string? status = null,
        string? search  = null, DateTime? from = null, DateTime? to = null,
        int page = 1)
    {
        const int pageSize = 25;
        var q = _db.CommunicationLogs.AsQueryable();

        if (!string.IsNullOrEmpty(channel)) q = q.Where(l => l.Channel == channel);
        if (!string.IsNullOrEmpty(status))  q = q.Where(l => l.Status  == status);
        if (from.HasValue) q = q.Where(l => l.CreatedAt >= from.Value.ToUniversalTime());
        if (to.HasValue)   q = q.Where(l => l.CreatedAt <= to.Value.ToUniversalTime().AddDays(1));
        if (!string.IsNullOrEmpty(search))
            q = q.Where(l => (l.RecipientName  != null && l.RecipientName.Contains(search))
                           || (l.RecipientPhone != null && l.RecipientPhone.Contains(search))
                           || (l.Subject.Contains(search)));

        var total = await q.CountAsync();
        var items = await q.OrderByDescending(l => l.CreatedAt)
                           .Skip((page - 1) * pageSize).Take(pageSize)
                           .ToListAsync();

        ViewBag.Channel   = channel;
        ViewBag.Status    = status;
        ViewBag.Search    = search;
        ViewBag.From      = from?.ToString("yyyy-MM-dd");
        ViewBag.To        = to?.ToString("yyyy-MM-dd");
        ViewBag.Page      = page;
        ViewBag.PageSize  = pageSize;
        ViewBag.TotalCount= total;
        return View(items);
    }

    public async Task<IActionResult> Details(Guid id)
    {
        var log = await _db.CommunicationLogs
            .Include(l => l.Recipient)
            .FirstOrDefaultAsync(l => l.Id == id);
        if (log == null) return NotFound();
        return View(log);
    }
}

// ═══════════════════════════════════════════════════════════════════
// MESSAGES — admin sends messages to customers / store owners
// ═══════════════════════════════════════════════════════════════════
[Authorize(Policy = "AdminOnly")]
public class MessagesController : Controller
{
    private readonly AdminDbContext _db;
    public MessagesController(AdminDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> Send(Guid? userId = null, Guid? orderId = null, Guid? disputeId = null)
    {
        ViewBag.UserId    = userId;
        ViewBag.OrderId   = orderId;
        ViewBag.DisputeId = disputeId;

        // Pre-populate recipient info if userId is given
        if (userId.HasValue)
        {
            var u = await _db.Users.FindAsync(userId.Value);
            ViewBag.RecipientName  = u?.FullName;
            ViewBag.RecipientPhone = u?.Phone;
            ViewBag.RecipientEmail = u?.Email;
        }
        return View();
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Send(
        Guid? recipientUserId, string? recipientName, string? recipientPhone,
        string channel, string subject, string body,
        Guid? relatedOrderId, Guid? relatedDisputeId)
    {
        if (string.IsNullOrWhiteSpace(subject) || string.IsNullOrWhiteSpace(body))
        {
            TempData["Error"] = "Subject and body are required.";
            return RedirectToAction(nameof(Send));
        }

        var adminId = Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var aid) ? aid : (Guid?)null;

        // Resolve recipient details if not provided directly
        if (recipientUserId.HasValue)
        {
            var u = await _db.Users.FindAsync(recipientUserId.Value);
            recipientName  ??= u?.FullName;
            recipientPhone ??= u?.Phone;
        }

        var log = new CommunicationLog
        {
            Id                = Guid.NewGuid(),
            Channel           = channel ?? "InApp",
            Status            = "Sent",   // log-only for now (SMS/Email may not be configured)
            RecipientUserId   = recipientUserId,
            RecipientName     = recipientName,
            RecipientPhone    = recipientPhone,
            Subject           = subject.Trim(),
            Body              = body.Trim(),
            RelatedEntityType = relatedOrderId.HasValue ? "Order" : relatedDisputeId.HasValue ? "Dispute" : null,
            RelatedEntityId   = relatedOrderId ?? relatedDisputeId,
            SentByAdminId     = adminId,
            SentAt            = DateTime.UtcNow,
            CreatedAt         = DateTime.UtcNow
        };

        _db.CommunicationLogs.Add(log);
        await _db.SaveChangesAsync();

        TempData["Success"] = $"Message logged as '{channel}' to {recipientName ?? recipientPhone ?? "user"}.";
        return RedirectToAction("Index", "CommunicationLogs");
    }
}

// ═══════════════════════════════════════════════════════════════════
// ADMIN USERS — SuperAdmin-only user management
// ═══════════════════════════════════════════════════════════════════
[Authorize(Roles = "SuperAdmin")]
public class AdminUsersController : VyronAdminController
{
    private readonly IAdminUserRepo _repo;
    public AdminUsersController(IAdminUserRepo repo) => _repo = repo;

    public async Task<IActionResult> Index() => View(await _repo.GetAllAdminUsersAsync());

    public IActionResult Create() => View(new AdminUserCreateVm());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(AdminUserCreateVm vm)
    {
        if (!ModelState.IsValid) return View(vm);
        var role = vm.IsAdminUser ? UserRole.AdminUser : UserRole.Admin;
        var (id, err) = await _repo.CreateAsync(vm.FullName, vm.Phone, vm.Email ?? "", vm.Password, role);
        if (id.HasValue) { TempData["Success"] = $"Admin user {vm.FullName} created."; return RedirectToAction("Index"); }
        ModelState.AddModelError("", err!);
        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Deactivate(Guid id)
    {
        await _repo.DeactivateAsync(id, CurrentUserId);
        TempData["Success"] = "Admin user deactivated.";
        return RedirectToAction("Index");
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Reactivate(Guid id)
    {
        await _repo.ReactivateAsync(id);
        TempData["Success"] = "Admin user reactivated.";
        return RedirectToAction("Index");
    }
}

// ═══════════════════════════════════════════════════════════════════
// STOREOWNER APPROVALS — SuperAdmin/Admin approves StoreOwner registrations
// ═══════════════════════════════════════════════════════════════════
[Authorize(Roles = "Admin,SuperAdmin")]
public class StoreOwnerApprovalsController : VyronAdminController
{
    private readonly IStoreOwnerApprovalRepo _repo;
    public StoreOwnerApprovalsController(IStoreOwnerApprovalRepo repo) => _repo = repo;

    public async Task<IActionResult> Index() => View(await _repo.GetAllApplicantsAsync());
    public async Task<IActionResult> Pending() => View(await _repo.GetPendingAsync());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(Guid id)
    {
        var (ok, err) = await _repo.ApproveAsync(id, CurrentUserId);
        if (ok) TempData["Success"] = "StoreOwner approved.";
        else TempData["Error"] = err;
        return RedirectToAction("Index");
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(Guid id, string reason)
    {
        var (ok, err) = await _repo.RejectAsync(id, CurrentUserId, reason);
        if (ok) TempData["Success"] = "StoreOwner application rejected.";
        else TempData["Error"] = err;
        return RedirectToAction("Index");
    }
}

// ═══════════════════════════════════════════════════════════════════
// AUDIT LOGS — read-only compliance view
// ═══════════════════════════════════════════════════════════════════
[Authorize(Roles = "Admin,SuperAdmin")]
public class AuditLogsController : VyronAdminController
{
    private readonly IAuditLogRepo _repo;
    public AuditLogsController(IAuditLogRepo repo) => _repo = repo;

    public async Task<IActionResult> Index(string? search, int page = 1)
    {
        ViewBag.Search = search; ViewBag.Page = page;
        ViewBag.Total = await _repo.CountAsync(search);
        return View(await _repo.GetAllAsync(search, page));
    }
}

// ═══════════════════════════════════════════════════════════════════
// ACTIVITY LOGS — read-only activity trail
// ═══════════════════════════════════════════════════════════════════
[Authorize(Roles = "Admin,SuperAdmin")]
public class ActivityLogsController : VyronAdminController
{
    private readonly IActivityLogRepo _repo;
    public ActivityLogsController(IActivityLogRepo repo) => _repo = repo;

    public async Task<IActionResult> Index(string? search, int page = 1)
    {
        ViewBag.Search = search; ViewBag.Page = page;
        ViewBag.Total = await _repo.CountAsync(search);
        return View(await _repo.GetAllAsync(search, page));
    }
}

// ═══════════════════════════════════════════════════════════════════
// STORE STAFF — StoreOwner manages staff assignments
// ═══════════════════════════════════════════════════════════════════
[Authorize(Roles = "StoreOwner,Admin,SuperAdmin")]
public class StoreStaffController : VyronAdminController
{
    private readonly IStoreStaffRepo _staff;
    private readonly IStoreRepo _stores;

    public StoreStaffController(IStoreStaffRepo staff, IStoreRepo stores)
    { _staff = staff; _stores = stores; }

    public async Task<IActionResult> Index()
    {
        var assignments = IsAdmin
            ? await _staff.GetByStoreAsync(Guid.Empty)   // TODO: admin view all — for now StoreOwner-scoped
            : await _staff.GetByOwnerAsync(CurrentUserId);
        return View(assignments);
    }

    public async Task<IActionResult> Create()
    {
        ViewBag.Stores = await _stores.GetByOwnerAsync(CurrentUserId);
        return View(new StoreStaffCreateVm());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(StoreStaffCreateVm vm)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.Stores = await _stores.GetByOwnerAsync(CurrentUserId);
            return View(vm);
        }
        var role = vm.IsManager ? UserRole.StoreManager : UserRole.StoreStaff;
        var (userId, err) = await _staff.CreateStaffAsync(
            vm.FullName, vm.Phone, vm.Password, role, vm.StoreId, CurrentUserId);
        if (userId.HasValue) { TempData["Success"] = $"Staff member {vm.FullName} created and assigned."; return RedirectToAction("Index"); }
        ModelState.AddModelError("", err!);
        ViewBag.Stores = await _stores.GetByOwnerAsync(CurrentUserId);
        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Revoke(Guid id)
    {
        await _staff.RevokeAsync(id, CurrentUserId);
        TempData["Success"] = "Staff assignment revoked.";
        return RedirectToAction("Index");
    }
}

// ═══════════════════════════════════════════════════════════════════
// REVENUE — Admin revenue/payment summary (MVP)
// ═══════════════════════════════════════════════════════════════════
[Authorize(Roles = "Admin,SuperAdmin")]
public class RevenueController : VyronAdminController
{
    private readonly AdminDbContext _db;
    public RevenueController(AdminDbContext db) => _db = db;

    public async Task<IActionResult> Index(DateTime? from, DateTime? to)
    {
        var fromDate = from ?? DateTime.UtcNow.AddDays(-30);
        var toDate   = (to ?? DateTime.UtcNow).Date.AddDays(1); // end-of-day inclusive

        var orders = await _db.Orders
            .AsNoTracking()
            .Include(o => o.Store)
            .Where(o => o.CreatedAt >= fromDate && o.CreatedAt < toDate)
            .ToListAsync();

        var payments = await _db.Payments
            .AsNoTracking()
            .Include(p => p.Order).ThenInclude(o => o.Store)
            .Where(p => p.CreatedAt >= fromDate && p.CreatedAt < toDate && p.IsSuccessful)
            .ToListAsync();

        decimal Total(Order o) => o.ActualTotal > 0 ? o.ActualTotal : o.TotalEstimate;

        var commissionPct = 10m; // default — ideally from SystemConfig
        var cfg = await _db.SystemConfigs.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Key == "PlatformCommissionPercent");
        if (cfg != null && decimal.TryParse(cfg.Value, out var pct)) commissionPct = pct;

        var completedOrders = orders.Where(o => o.Status == Vyron.Shared.Enums.OrderStatus.Completed ||
                                                 o.PaymentState == Vyron.Shared.Enums.PaymentState.FullyPaid).ToList();
        var pendingOrders   = orders.Where(o => o.PaymentState == Vyron.Shared.Enums.PaymentState.Unpaid ||
                                                 o.PaymentState == Vyron.Shared.Enums.PaymentState.PickupFeePaid).ToList();

        var totalOrderValue     = orders.Sum(Total);
        var completedRevenue    = completedOrders.Sum(Total);
        var pickupFeesCollected = payments.Where(p => p.Type == "pickup_fee").Sum(p => p.Amount);
        var deliveryFees        = orders.Sum(o => o.DeliveryFee);
        var commission          = Math.Round(completedRevenue * commissionPct / 100m, 2);
        var storePayoutEstimate = completedRevenue - commission;

        ViewBag.From            = fromDate.ToString("yyyy-MM-dd");
        ViewBag.To              = (toDate.AddDays(-1)).ToString("yyyy-MM-dd");
        ViewBag.TotalOrderValue      = totalOrderValue;
        ViewBag.CompletedRevenue     = completedRevenue;
        ViewBag.PickupFeesCollected  = pickupFeesCollected;
        ViewBag.DeliveryFees         = deliveryFees;
        ViewBag.CommissionPct        = commissionPct;
        ViewBag.CommissionEstimate   = commission;
        ViewBag.StorePayoutEstimate  = storePayoutEstimate;
        ViewBag.PendingCount         = pendingOrders.Count;
        ViewBag.CompletedCount       = completedOrders.Count;
        ViewBag.CancelledCount       = orders.Count(o => o.Status == Vyron.Shared.Enums.OrderStatus.Cancelled);
        ViewBag.TotalOrders          = orders.Count;
        ViewBag.RecentPayments       = payments.OrderByDescending(p => p.CreatedAt).Take(20).ToList();

        ViewData["Title"] = "Revenue Summary";
        return View();
    }
}

// ═══════════════════════════════════════════════════════════════════
// REPORTS — MVP admin reporting module
// ═══════════════════════════════════════════════════════════════════
[Authorize(Roles = "Admin,SuperAdmin")]
public class ReportsController : VyronAdminController
{
    private readonly AdminDbContext _db;
    public ReportsController(AdminDbContext db) => _db = db;

    public IActionResult Index() { ViewData["Title"] = "Reports"; return View(); }

    public async Task<IActionResult> Users(DateTime? from, DateTime? to, string? role, int page = 1)
    {
        const int size = 25;
        var q = _db.Users.AsQueryable();
        if (from.HasValue)  q = q.Where(u => u.CreatedAt >= from.Value.ToUniversalTime());
        if (to.HasValue)    q = q.Where(u => u.CreatedAt <= to.Value.ToUniversalTime().AddDays(1));
        if (!string.IsNullOrEmpty(role) && Enum.TryParse<UserRole>(role, out var r))
            q = q.Where(u => u.Role == r);

        ViewBag.Total   = await q.CountAsync();
        ViewBag.Page    = page; ViewBag.From = from?.ToString("yyyy-MM-dd"); ViewBag.To = to?.ToString("yyyy-MM-dd"); ViewBag.Role = role;
        ViewData["Title"] = "User Report";
        return View(await q.OrderByDescending(u => u.CreatedAt).Skip((page - 1) * size).Take(size).AsNoTracking().ToListAsync());
    }

    public async Task<IActionResult> Orders(DateTime? from, DateTime? to, OrderStatus? status, Guid? storeId, int page = 1)
    {
        const int size = 25;
        var q = _db.Orders.Include(o => o.Customer).Include(o => o.Store).AsQueryable();
        if (from.HasValue)   q = q.Where(o => o.CreatedAt >= from.Value.ToUniversalTime());
        if (to.HasValue)     q = q.Where(o => o.CreatedAt <= to.Value.ToUniversalTime().AddDays(1));
        if (status.HasValue) q = q.Where(o => o.Status == status.Value);
        if (storeId.HasValue) q = q.Where(o => o.StoreId == storeId.Value);

        ViewBag.Total    = await q.CountAsync();
        ViewBag.Page     = page; ViewBag.From = from?.ToString("yyyy-MM-dd"); ViewBag.To = to?.ToString("yyyy-MM-dd");
        ViewBag.Status   = status; ViewBag.StoreId = storeId;
        ViewBag.Stores   = await _db.Stores.AsNoTracking().Select(s => new { s.Id, s.Name }).ToListAsync();
        ViewData["Title"] = "Order Report";
        return View(await q.OrderByDescending(o => o.CreatedAt).Skip((page - 1) * size).Take(size).AsNoTracking().ToListAsync());
    }

    public async Task<IActionResult> Stores(int page = 1)
    {
        const int size = 25;
        var q = _db.Stores.Include(s => s.Owner).AsQueryable();
        ViewBag.Total = await q.CountAsync(); ViewBag.Page = page;
        ViewData["Title"] = "Store Report";
        return View(await q.OrderByDescending(s => s.CreatedAt).Skip((page - 1) * size).Take(size).AsNoTracking().ToListAsync());
    }

    public async Task<IActionResult> Payments(DateTime? from, DateTime? to, int page = 1)
    {
        const int size = 25;
        var q = _db.Payments.Include(p => p.Order).ThenInclude(o => o.Customer).AsQueryable();
        if (from.HasValue) q = q.Where(p => p.CreatedAt >= from.Value.ToUniversalTime());
        if (to.HasValue)   q = q.Where(p => p.CreatedAt <= to.Value.ToUniversalTime().AddDays(1));
        ViewBag.Total = await q.CountAsync(); ViewBag.Page = page; ViewBag.From = from?.ToString("yyyy-MM-dd"); ViewBag.To = to?.ToString("yyyy-MM-dd");
        ViewData["Title"] = "Payment Report";
        return View(await q.OrderByDescending(p => p.CreatedAt).Skip((page - 1) * size).Take(size).AsNoTracking().ToListAsync());
    }

    public async Task<IActionResult> AuditLogs(DateTime? from, DateTime? to, string? search, int page = 1)
    {
        const int size = 25;
        var q = _db.AuditLogs.Include(a => a.User).AsQueryable();
        if (from.HasValue) q = q.Where(a => a.CreatedAt >= from.Value.ToUniversalTime());
        if (to.HasValue)   q = q.Where(a => a.CreatedAt <= to.Value.ToUniversalTime().AddDays(1));
        if (!string.IsNullOrEmpty(search))
            q = q.Where(a => a.Action.Contains(search) || a.Entity.Contains(search) ||
                             (a.User != null && a.User.FullName.Contains(search)));
        ViewBag.Total = await q.CountAsync(); ViewBag.Page = page; ViewBag.From = from?.ToString("yyyy-MM-dd"); ViewBag.To = to?.ToString("yyyy-MM-dd"); ViewBag.Search = search;
        ViewData["Title"] = "Audit Log Report";
        return View(await q.OrderByDescending(a => a.CreatedAt).Skip((page - 1) * size).Take(size).AsNoTracking().ToListAsync());
    }

    public async Task<IActionResult> Compliance(DateTime? from, DateTime? to)
    {
        var fromDate = from ?? DateTime.UtcNow.AddDays(-30);
        var toDate   = (to ?? DateTime.UtcNow).Date.AddDays(1);

        ViewBag.TotalOrders       = await _db.Orders.CountAsync(o => o.CreatedAt >= fromDate && o.CreatedAt < toDate);
        ViewBag.CancelledOrders   = await _db.Orders.CountAsync(o => o.Status == OrderStatus.Cancelled && o.CreatedAt >= fromDate && o.CreatedAt < toDate);
        ViewBag.DisputedOrders    = await _db.Orders.CountAsync(o => o.Status == OrderStatus.Disputed && o.CreatedAt >= fromDate && o.CreatedAt < toDate);
        ViewBag.OpenDisputes      = await _db.Disputes.CountAsync(d => d.Status == DisputeStatus.Open);
        ViewBag.ResolvedDisputes  = await _db.Disputes.CountAsync(d => d.Status == DisputeStatus.Resolved || d.Status == DisputeStatus.Refunded);
        ViewBag.AuditEventCount   = await _db.AuditLogs.CountAsync(a => a.CreatedAt >= fromDate && a.CreatedAt < toDate);
        ViewBag.CommLogs          = await _db.CommunicationLogs.CountAsync(l => l.CreatedAt >= fromDate && l.CreatedAt < toDate);
        ViewBag.From              = fromDate.ToString("yyyy-MM-dd");
        ViewBag.To                = (toDate.AddDays(-1)).ToString("yyyy-MM-dd");
        ViewData["Title"]         = "Compliance Report";
        return View();
    }
}

// ═══════════════════════════════════════════════════════════════════
// COUPONS — Admin coupon/promo management
// ═══════════════════════════════════════════════════════════════════
[Authorize(Roles = "Admin,SuperAdmin")]
public class CouponsController : VyronAdminController
{
    private readonly AdminDbContext _db;
    public CouponsController(AdminDbContext db) => _db = db;

    public async Task<IActionResult> Index()
    {
        ViewData["Title"] = "Coupons";
        return View(await _db.Coupons.AsNoTracking().OrderByDescending(c => c.CreatedAt).ToListAsync());
    }

    public IActionResult Create()
    {
        ViewData["Title"] = "Create Coupon";
        return View(new Vyron.API.Models.Coupon());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Vyron.API.Models.Coupon model)
    {
        if (!ModelState.IsValid) { ViewData["Title"] = "Create Coupon"; return View(model); }
        if (await _db.Coupons.AnyAsync(c => c.Code == model.Code.ToUpperInvariant()))
        {
            ModelState.AddModelError("Code", "A coupon with this code already exists.");
            ViewData["Title"] = "Create Coupon";
            return View(model);
        }
        model.Id = Guid.NewGuid();
        model.Code = model.Code.ToUpperInvariant().Trim();
        model.CreatedAt = model.UpdatedAt = DateTime.UtcNow;
        model.CreatedByUserId = CurrentUserId;
        _db.Coupons.Add(model);
        await _db.SaveChangesAsync();
        TempData["Success"] = $"Coupon {model.Code} created.";
        return RedirectToAction("Index");
    }

    public async Task<IActionResult> Edit(Guid id)
    {
        var c = await _db.Coupons.FindAsync(id);
        if (c == null) return NotFound();
        ViewData["Title"] = "Edit Coupon";
        return View(c);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, Vyron.API.Models.Coupon model)
    {
        if (!ModelState.IsValid) { ViewData["Title"] = "Edit Coupon"; return View(model); }
        var c = await _db.Coupons.FindAsync(id);
        if (c == null) return NotFound();
        c.Code                  = model.Code.ToUpperInvariant().Trim();
        c.Description           = model.Description;
        c.DiscountType          = model.DiscountType;
        c.DiscountValue         = model.DiscountValue;
        c.MaxUsesPerCustomer    = model.MaxUsesPerCustomer;
        c.GlobalMaxUses         = model.GlobalMaxUses;
        c.StartDate             = model.StartDate;
        c.EndDate               = model.EndDate;
        c.IsActive              = model.IsActive;
        c.AppliesToFirstNOrders = model.AppliesToFirstNOrders;
        c.UpdatedAt             = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        TempData["Success"] = $"Coupon {c.Code} updated.";
        return RedirectToAction("Index");
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Toggle(Guid id)
    {
        var c = await _db.Coupons.FindAsync(id);
        if (c == null) return NotFound();
        c.IsActive = !c.IsActive;
        c.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        TempData["Success"] = c.IsActive ? $"{c.Code} activated." : $"{c.Code} deactivated.";
        return RedirectToAction("Index");
    }
}
