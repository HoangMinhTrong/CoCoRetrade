using CoCoReTrade.Application;
using CoCoReTrade.Infrastructure;
using CoCoReTrade.Web.Realtime;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Localization;
using System.Globalization;
using System.Text.Json.Serialization;

var displayCulture = (CultureInfo)CultureInfo.GetCultureInfo("vi-VN").Clone();
displayCulture.NumberFormat.NumberGroupSeparator = ",";
displayCulture.NumberFormat.NumberDecimalSeparator = ".";
displayCulture.NumberFormat.CurrencyGroupSeparator = ",";
displayCulture.NumberFormat.CurrencyDecimalSeparator = ".";
CultureInfo.DefaultThreadCurrentCulture = displayCulture;
CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.GetCultureInfo("vi-VN");

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("CoCo")
    ?? throw new InvalidOperationException("Hãy cấu hình chuỗi kết nối ConnectionStrings:CoCo.");
var previewMode = builder.Environment.IsDevelopment() && builder.Configuration.GetValue<bool>("Preview:Enabled");

builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlServer(connectionString));
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequiredLength = 8;
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.User.RequireUniqueEmail = true;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    options.Lockout.MaxFailedAccessAttempts = 5;
}).AddEntityFrameworkStores<AppDbContext>().AddDefaultTokenProviders()
    .AddErrorDescriber<VietnameseIdentityErrorDescriber>();
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.Cookie.Name = "CoCo.Auth";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Events.OnRedirectToLogin = context =>
    {
        if (context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        else context.Response.Redirect(context.RedirectUri);
        return Task.CompletedTask;
    };
    options.Events.OnRedirectToAccessDenied = context =>
    {
        if (context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
        else context.Response.Redirect(context.RedirectUri);
        return Task.CompletedTask;
    };
});
builder.Services.Configure<SecurityStampValidatorOptions>(options => options.ValidationInterval = TimeSpan.Zero);
builder.Services.AddAntiforgery(options => options.HeaderName = "X-CSRF-TOKEN");
builder.Services.AddSignalR();
builder.Services.AddAuthorization(options =>
{
    foreach (var permission in new[] { StaffPermissions.ReadPending, StaffPermissions.Inspect,
        StaffPermissions.Publish, StaffPermissions.Reject, StaffPermissions.ManageProducts,
        StaffPermissions.ManageOrders, StaffPermissions.ManageUsers, StaffPermissions.ManageWallets,
        StaffPermissions.ReadAudit })
        options.AddPolicy(permission, policy => policy.RequireAuthenticatedUser()
            .RequireRole(StaffPermissions.RequiredRoles(permission))
            .RequireClaim(StaffPermissions.ClaimType, permission));
});
builder.Services.AddControllersWithViews(options =>
    {
        options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
        options.Filters.Add(new CoCoReTrade.Web.AuthorizationExceptionFilter());
        options.ModelBindingMessageProvider.SetValueIsInvalidAccessor(value => $"Giá trị '{value}' không hợp lệ.");
        options.ModelBindingMessageProvider.SetValueMustBeANumberAccessor(name => $"{name} phải là số.");
        options.ModelBindingMessageProvider.SetNonPropertyValueMustBeANumberAccessor(() => "Giá trị phải là số.");
        options.ModelBindingMessageProvider.SetAttemptedValueIsInvalidAccessor((value, name) => $"Giá trị '{value}' của {name} không hợp lệ.");
        options.ModelBindingMessageProvider.SetUnknownValueIsInvalidAccessor(name => $"Giá trị của {name} không hợp lệ.");
        options.ModelBindingMessageProvider.SetValueMustNotBeNullAccessor(name => $"Cần nhập {name}.");
    })
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddScoped<IProductRepository, ProductRepository>();
builder.Services.AddScoped<IProductCardMetadataRepository, ProductRepository>();
builder.Services.AddScoped<ISocialRepository, SocialRepository>();
builder.Services.AddScoped<ISocialService, SocialService>();
builder.Services.AddScoped<ICartRepository, CartRepository>();
builder.Services.AddScoped<ICartService, CartService>();
builder.Services.AddScoped<IConversationRepository, ConversationRepository>();
builder.Services.AddScoped<IConversationService, ConversationService>();
builder.Services.AddSingleton<IConversationRealtimePublisher, SignalRConversationRealtimePublisher>();
builder.Services.AddScoped<ISupportRepository, SupportRepository>();
builder.Services.AddScoped<ISupportService, SupportService>();
builder.Services.AddSingleton<ISupportRealtimePublisher, SignalRSupportRealtimePublisher>();
builder.Services.AddScoped<IAddressRepository, AddressRepository>();
builder.Services.AddScoped<IAddressBookService, AddressBookService>();
builder.Services.AddScoped<IUserProfileRepository, UserProfileRepository>();
builder.Services.AddScoped<IUserProfileService, UserProfileService>();
builder.Services.AddScoped<INotificationCenterRepository, NotificationCenterRepository>();
builder.Services.AddScoped<INotificationCenter, NotificationCenter>();
builder.Services.AddScoped<IUnreadCountsRepository, UnreadCountsRepository>();
builder.Services.AddScoped<IUnreadCountsService, UnreadCountsService>();
builder.Services.AddScoped<IProductCodeSequence, SqlProductCodeSequence>();
builder.Services.AddScoped<IProductCodeService, ProductCodeService>();
builder.Services.AddScoped<IStaffPermissionChecker, StaffPermissionChecker>();
builder.Services.AddScoped<IProductLifecycleService, ProductLifecycleService>();
builder.Services.AddScoped<IOrderLifecycleService, OrderLifecycleService>();
builder.Services.AddScoped<IAdminProductRepository, AdminProductRepository>();
builder.Services.AddScoped<IAdminProductService, AdminProductService>();
builder.Services.AddScoped<IAdminUserRepository, AdminUserRepository>();
builder.Services.AddScoped<IAdminUserService, AdminUserService>();
builder.Services.AddScoped<IAdminWalletRepository, AdminWalletRepository>();
builder.Services.AddScoped<IPlatformCommissionRepository, PlatformCommissionRepository>();
builder.Services.AddScoped<IPlatformCommissionService, PlatformCommissionService>();
builder.Services.AddScoped<IAdminFinancialReportRepository, AdminFinancialReportRepository>();
builder.Services.AddScoped<IAdminFinancialReportService, AdminFinancialReportService>();
builder.Services.AddScoped<ISellerPayoutService, SellerPayoutService>();
builder.Services.AddScoped<IAdminWalletService, AdminWalletService>();
builder.Services.AddScoped<IWalletWithdrawalRepository, WalletWithdrawalRepository>();
builder.Services.AddScoped<IWalletService, WalletService>();
builder.Services.AddScoped<IAdminOrderService, AdminOrderService>();
builder.Services.AddScoped<IAdminHeldPickupRepository, AdminHeldPickupRepository>();
builder.Services.AddScoped<IPrivateImageStore, PrivateImageStore>();
if (previewMode) builder.Services.AddSingleton<IProductService, CoCoReTrade.Web.Preview.PreviewProductService>();
else builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<IAccountService, AccountService>();
builder.Services.AddScoped<IAccountOtpService, AccountOtpService>();
builder.Services.AddScoped<IAccountMessageSender, SmtpAccountMessageSender>();
builder.Services.AddScoped<IOfferRepository, OfferRepository>();
builder.Services.AddScoped<IOfferService, OfferService>();
builder.Services.AddScoped<IOfferExpirationService, OfferExpirationService>();
builder.Services.AddScoped<IOrderRepository, OrderRepository>();
builder.Services.AddScoped<IAcceptedPickupExpirationRepository, OrderRepository>();
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<IOrderExpirationService, OrderExpirationService>();
builder.Services.AddScoped<IShippingZoneRepository, ShippingZoneRepository>();
builder.Services.AddScoped<IShippingFeeService, ShippingFeeService>();
builder.Services.AddScoped<ICommissionService, CommissionService>();
builder.Services.AddScoped<INotificationRepository, NotificationRepository>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IOutboxRepository, OutboxRepository>();
builder.Services.AddScoped<IOutboxDispatcher, OutboxDispatcher>();
builder.Services.AddScoped<IListingReminderService, ListingReminderService>();
if (!string.IsNullOrWhiteSpace(builder.Configuration["Smtp:Host"]) &&
    !string.IsNullOrWhiteSpace(builder.Configuration["Smtp:From"]))
    builder.Services.AddScoped<IEmailNotificationProvider, SmtpMarketplaceEmailProvider>();
if (!previewMode)
{
    builder.Services.AddHostedService<OfferExpirationWorker>();
    builder.Services.AddHostedService<OrderExpirationWorker>();
    builder.Services.AddHostedService<OutboxDispatcherWorker>();
    builder.Services.AddHostedService<ListingReminderWorker>();
}
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton(new TrustPolicy(builder.Configuration.GetValue<int?>("Trust:MinimumScore") ?? 30));
var discount = int.TryParse(builder.Configuration["Marketplace:MaximumOfferDiscountPercent"], out var configuredDiscount) ? configuredDiscount : 25;
var commission = int.TryParse(builder.Configuration["Marketplace:CommissionPercent"], out var configuredCommission) ? configuredCommission : 15;
builder.Services.AddSingleton(new MarketplacePolicy(discount, commission));
builder.Services.AddSingleton(new OrderExpirationPolicy(
    builder.Configuration.GetValue<int?>("Orders:UnpaidReservationMinutes") ?? 1440));
var innerZones = builder.Configuration.GetSection("Shipping:InnerCityZones").GetChildren()
    .Select(x => new ShippingZone(x["Province"] ?? "", x["Ward"] ?? "")).ToList();
builder.Services.AddSingleton(new ShippingPolicy(innerZones,
    builder.Configuration.GetValue<int?>("Shipping:StoreUtcOffsetHours") ?? 7,
    builder.Configuration.GetValue<int?>("Shipping:OpeningHour") ?? 9,
    builder.Configuration.GetValue<int?>("Shipping:ClosingHour") ?? 21,
    builder.Configuration["Shipping:StoreAddress"] ?? ""));

var app = builder.Build();
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture(displayCulture, CultureInfo.GetCultureInfo("vi-VN")),
    SupportedCultures = [displayCulture],
    SupportedUICultures = [CultureInfo.GetCultureInfo("vi-VN")]
});
if (!app.Environment.IsDevelopment()) { app.UseExceptionHandler("/Home/Error"); app.UseHsts(); }
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
if (previewMode)
{
    app.Use(async (context, next) =>
    {
        if (!HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method))
        {
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            await context.Response.WriteAsync("Preview only. Configure SQL Server to enable accounts and transactions.");
            return;
        }
        await next();
    });
}
else app.UseAuthentication();
app.Use(async (context, next) =>
{
    if (context.User.Identity?.IsAuthenticated == true &&
        !context.Request.Path.StartsWithSegments("/Account/ChangePassword", StringComparison.OrdinalIgnoreCase) &&
        !context.Request.Path.StartsWithSegments("/Account/Logout", StringComparison.OrdinalIgnoreCase) &&
        !context.Request.Path.StartsWithSegments("/api/v1/session", StringComparison.OrdinalIgnoreCase) &&
        !Path.HasExtension(context.Request.Path))
    {
        var users = context.RequestServices.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await users.GetUserAsync(context.User);
        if (user?.TrustSuspended == true)
        {
            await context.RequestServices.GetRequiredService<SignInManager<ApplicationUser>>().SignOutAsync();
            context.Response.Redirect("/Account/AccessDenied");
            return;
        }
        if (user?.MustChangePassword == true)
        {
            if (context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
            { context.Response.StatusCode = StatusCodes.Status403Forbidden; await context.Response.WriteAsJsonAsync(new { error = "Đổi mật khẩu trước khi tiếp tục." }); }
            else context.Response.Redirect("/Account/ChangePassword");
            return;
        }
    }
    await next();
});
app.UseAuthorization();
app.MapHub<ConversationHub>("/hubs/conversations");
app.MapHub<SupportHub>("/hubs/support");
app.MapControllers();
app.MapControllerRoute("areas", "{area:exists}/{controller=Inspection}/{action=Index}/{id?}");
app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");

if (!previewMode) using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await IdentitySeeder.SeedAsync(db,
        scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>(),
        scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>(),
        scope.ServiceProvider.GetRequiredService<IConfiguration>());
    await ShippingZoneSeeder.SeedAsync(db, scope.ServiceProvider.GetRequiredService<IConfiguration>());
}
app.Run();

public partial class Program { }
