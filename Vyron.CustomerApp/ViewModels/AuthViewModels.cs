using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vyron.CustomerApp.DTOs;
using Vyron.CustomerApp.Helpers;
using Vyron.CustomerApp.Models;
using Vyron.CustomerApp.Services;

namespace Vyron.CustomerApp.ViewModels;

// ─── LOGIN (phone + password for returning customers) ─────────────
public partial class LoginViewModel : BaseViewModel
{
    private readonly IAuthService _auth;

    public IReadOnlyList<CountryCodeOption> CountryOptions => PhoneHelper.CountryOptions;

    [ObservableProperty] private CountryCodeOption _selectedCountry = PhoneHelper.DefaultCountry;
    [ObservableProperty] private string _localPhone   = "";
    [ObservableProperty] private string _password     = "";
    [ObservableProperty] private bool   _showPassword  = false;
    [ObservableProperty] private bool   _rememberMe    = false;

    [RelayCommand]
    private void ToggleRememberMe() => RememberMe = !RememberMe;

    public string PasswordToggleIcon => ShowPassword ? "👁️" : "🔒";

    public LoginViewModel(IAuthService auth) => _auth = auth;

    partial void OnLocalPhoneChanged(string value)
    {
        var clean = PhoneHelper.SanitizeLocalInput(value, SelectedCountry.DialCode);
        if (clean != value) LocalPhone = clean;
    }

    [RelayCommand]
    private void TogglePassword()
    {
        ShowPassword = !ShowPassword;
        OnPropertyChanged(nameof(PasswordToggleIcon));
    }

    [RelayCommand]
    private async Task LoginAsync()
    {
        if (IsBusy) return;
        ErrorMessage = null;

        if (string.IsNullOrWhiteSpace(LocalPhone))
        { SetError("Please enter your phone number."); return; }

        var phone = PhoneHelper.Normalize(LocalPhone.Trim(), SelectedCountry.DialCode);
        if (!PhoneHelper.IsValid(phone))
        { SetError("Please enter a valid phone number (digits only, e.g. 8012345678)."); return; }

        if (string.IsNullOrWhiteSpace(Password))
        { SetError("Please enter your password."); return; }

        if (!ApiErrorHelper.HasInternetAccess)
        {
            SetError(ApiErrorHelper.OfflineMessage);
            return;
        }

        IsBusy = true;
        try
        {
            var (ok, error) = await _auth.LoginAsync(phone, Password, RememberMe);

            if (ok)
                await Shell.Current.GoToAsync(AppRoutes.Home, animate: false);
            else
                SetError(error ?? "Login failed. Please check your credentials.");
        }
        catch (Exception ex)
        {
            SetError(ApiErrorHelper.ForException(ex));
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task GoToSignupAsync()
        => await Shell.Current.GoToAsync(AppRoutes.Signup);

    [RelayCommand]
    private async Task GoToForgotPasswordAsync()
        => await Shell.Current.GoToAsync(AppRoutes.ForgotPassword);
}

// ─── SIGNUP (step 1: phone entry — new customers only) ────────────
public partial class SignupViewModel : BaseViewModel
{
    private readonly IAuthService _auth;

    public IReadOnlyList<CountryCodeOption> CountryOptions => PhoneHelper.CountryOptions;

    [ObservableProperty] private CountryCodeOption _selectedCountry = PhoneHelper.DefaultCountry;
    [ObservableProperty] private string _localPhone = "";

    public SignupViewModel(IAuthService auth) => _auth = auth;

    partial void OnLocalPhoneChanged(string value)
    {
        var clean = PhoneHelper.SanitizeLocalInput(value, SelectedCountry.DialCode);
        if (clean != value) LocalPhone = clean;
    }

    [RelayCommand]
    private async Task RequestOtpAsync()
    {
        if (IsBusy) return;
        ErrorMessage = null;

        if (string.IsNullOrWhiteSpace(LocalPhone))
        { SetError("Please enter your phone number."); return; }

        var phone = PhoneHelper.Normalize(LocalPhone.Trim(), SelectedCountry.DialCode);
        if (!PhoneHelper.IsValid(phone))
        { SetError("Please enter a valid phone number (digits only, e.g. 8012345678)."); return; }

        IsBusy = true;
        var (ok, devOtp, error) = await _auth.RequestSignupOtpAsync(phone);
        IsBusy = false;

        if (ok)
        {
            var url = $"{AppRoutes.CompleteProfile}?phone={Uri.EscapeDataString(phone)}";
            if (!string.IsNullOrEmpty(devOtp))
                url += $"&devOtp={Uri.EscapeDataString(devOtp)}";
            await Shell.Current.GoToAsync(url);
        }
        else
        {
            SetError(error ?? "Failed to send verification code. Please try again.");
        }
    }
}

// ─── COMPLETE PROFILE (step 2: OTP + profile + password) ──────────
[QueryProperty(nameof(Phone),  "phone")]
[QueryProperty(nameof(DevOtp), "devOtp")]
public partial class CompleteProfileViewModel : BaseViewModel
{
    private readonly IAuthService _auth;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDevOtp))]
    private string _devOtp = "";

    [ObservableProperty] private string _phone           = "";
    [ObservableProperty] private string _otpCode         = "";
    [ObservableProperty] private string _fullName        = "";
    [ObservableProperty] private string _email           = "";
    [ObservableProperty] private string _password        = "";
    [ObservableProperty] private string _confirmPassword = "";
    [ObservableProperty] private bool   _showPassword    = false;

    public bool HasDevOtp => !string.IsNullOrEmpty(DevOtp);
    public string PasswordToggleIcon => ShowPassword ? "👁️" : "🔒";

    public CompleteProfileViewModel(IAuthService auth) => _auth = auth;

    [RelayCommand]
    private void TogglePassword()
    {
        ShowPassword = !ShowPassword;
        OnPropertyChanged(nameof(PasswordToggleIcon));
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (IsBusy) return;
        ErrorMessage = null;

        if (string.IsNullOrWhiteSpace(OtpCode) || OtpCode.Length < 6)
        { SetError("Please enter the 6-digit verification code."); return; }

        if (string.IsNullOrWhiteSpace(FullName))
        { SetError("Please enter your full name."); return; }

        if (string.IsNullOrWhiteSpace(Password) || Password.Length < 6)
        { SetError("Password must be at least 6 characters."); return; }

        if (Password != ConfirmPassword)
        { SetError("Passwords do not match."); return; }

        IsBusy = true;
        var (ok, error) = await _auth.CompleteProfileAsync(
            Phone, OtpCode, FullName.Trim(),
            string.IsNullOrWhiteSpace(Email) ? null : Email.Trim(),
            Password);
        IsBusy = false;

        if (ok)
            await Shell.Current.GoToAsync(AppRoutes.Home, animate: false);
        else
            SetError(error ?? "Failed to create account. Please try again.");
    }
}

// ─── VERIFY OTP (legacy OTP flow — used by old send-otp path) ─────
[QueryProperty(nameof(Phone),       "phone")]
[QueryProperty(nameof(DevOtp),      "devOtp")]
[QueryProperty(nameof(FlowContext), "flowContext")]
public partial class VerifyOtpViewModel : BaseViewModel
{
    private readonly IAuthService _auth;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDevOtp))]
    private string _devOtp = "";

    [ObservableProperty] private string _phone       = "";
    [ObservableProperty] private string _otpCode     = "";
    [ObservableProperty] private string _flowContext = "";   // "signup" | "resetPassword" | "" (legacy)

    /// True only in Development when the API echoed the OTP back.
    public bool HasDevOtp => !string.IsNullOrEmpty(DevOtp);

    public VerifyOtpViewModel(IAuthService auth) => _auth = auth;

    [RelayCommand]
    private async Task VerifyAsync()
    {
        ErrorMessage = null;

        if (OtpCode.Length < 6)
        { SetError("Please enter the 6-digit verification code."); return; }

        IsBusy = true;
        var (ok, requiresProfile, error) = await _auth.VerifyOtpAsync(Phone, OtpCode);
        IsBusy = false;

        if (ok)
        {
            if (requiresProfile)
                await Shell.Current.GoToAsync(AppRoutes.CompleteProfile);
            else
                await Shell.Current.GoToAsync(AppRoutes.Stores, animate: false);
        }
        else
        {
            SetError(error ?? "Invalid code. Please try again.");
        }
    }

    [RelayCommand]
    private async Task ResendAsync()
    {
        ErrorMessage   = null;
        SuccessMessage = null;

        IsBusy = true;
        var (ok, newDevOtp, error) = await _auth.SendOtpAsync(Phone);
        IsBusy = false;

        if (ok)
        {
            OtpCode        = "";
            DevOtp         = newDevOtp ?? "";
            SuccessMessage = "A new verification code has been sent.";
        }
        else
        {
            SetError(error ?? "Could not resend code. Please try again.");
        }
    }

    [RelayCommand]
    private async Task EditPhoneAsync()
        => await Shell.Current.GoToAsync("..");
}

// ─── FORGOT PASSWORD (step 1: phone entry) ────────────────────────
public partial class ForgotPasswordViewModel : BaseViewModel
{
    private readonly IAuthService _auth;

    public IReadOnlyList<CountryCodeOption> CountryOptions => PhoneHelper.CountryOptions;

    [ObservableProperty] private CountryCodeOption _selectedCountry = PhoneHelper.DefaultCountry;
    [ObservableProperty] private string _localPhone = "";

    public ForgotPasswordViewModel(IAuthService auth) => _auth = auth;

    partial void OnLocalPhoneChanged(string value)
    {
        var clean = PhoneHelper.SanitizeLocalInput(value, SelectedCountry.DialCode);
        if (clean != value) LocalPhone = clean;
    }

    [RelayCommand]
    private async Task RequestResetOtpAsync()
    {
        if (IsBusy) return;
        ErrorMessage = null;

        if (string.IsNullOrWhiteSpace(LocalPhone))
        { SetError("Please enter your phone number."); return; }

        var phone = PhoneHelper.Normalize(LocalPhone.Trim(), SelectedCountry.DialCode);
        if (!PhoneHelper.IsValid(phone))
        { SetError("Please enter a valid phone number."); return; }

        IsBusy = true;
        var (ok, devOtp, error) = await _auth.RequestPasswordResetAsync(phone);
        IsBusy = false;

        if (ok)
        {
            var url = $"{AppRoutes.ResetPassword}?phone={Uri.EscapeDataString(phone)}";
            if (!string.IsNullOrEmpty(devOtp))
                url += $"&devOtp={Uri.EscapeDataString(devOtp)}";
            await Shell.Current.GoToAsync(url);
        }
        else
        {
            SetError(error ?? "Failed to send reset code. Please try again.");
        }
    }

    [RelayCommand]
    private async Task GoBackAsync()
        => await Shell.Current.GoToAsync("..");
}

// ─── RESET PASSWORD (step 2: OTP + new password) ──────────────────
[QueryProperty(nameof(Phone),  "phone")]
[QueryProperty(nameof(DevOtp), "devOtp")]
public partial class ResetPasswordViewModel : BaseViewModel
{
    private readonly IAuthService _auth;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDevOtp))]
    private string _devOtp = "";

    [ObservableProperty] private string _phone           = "";
    [ObservableProperty] private string _otpCode         = "";
    [ObservableProperty] private string _newPassword     = "";
    [ObservableProperty] private string _confirmPassword = "";
    [ObservableProperty] private bool   _showPassword    = false;

    public bool HasDevOtp => !string.IsNullOrEmpty(DevOtp);
    public string PasswordToggleIcon => ShowPassword ? "👁️" : "🔒";

    public ResetPasswordViewModel(IAuthService auth) => _auth = auth;

    [RelayCommand]
    private void TogglePassword()
    {
        ShowPassword = !ShowPassword;
        OnPropertyChanged(nameof(PasswordToggleIcon));
    }

    [RelayCommand]
    private async Task ResetAsync()
    {
        if (IsBusy) return;
        ErrorMessage = null;

        if (string.IsNullOrWhiteSpace(OtpCode) || OtpCode.Length < 6)
        { SetError("Please enter the 6-digit verification code."); return; }

        if (string.IsNullOrWhiteSpace(NewPassword) || NewPassword.Length < 6)
        { SetError("Password must be at least 6 characters."); return; }

        if (NewPassword != ConfirmPassword)
        { SetError("Passwords do not match."); return; }

        IsBusy = true;
        var (ok, error) = await _auth.ResetPasswordAsync(Phone, OtpCode, NewPassword);
        IsBusy = false;

        if (ok)
        {
            SuccessMessage = "Password reset successfully! Please login with your new password.";
            await Task.Delay(1500);
            await Shell.Current.GoToAsync(AppRoutes.Login, animate: false);
        }
        else
        {
            SetError(error ?? "Failed to reset password. Please try again.");
        }
    }
}
