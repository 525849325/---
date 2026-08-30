using ImmortalLoot.Server.Persistence;
using ImmortalLoot.Server.Services;
using ImmortalLoot.Server.Config;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
// The packaged server may be launched from the repository, a service manager, or
// its publish directory. Always overlay settings located beside the executable so
// environment-specific security switches do not depend on the caller's cwd.
builder.Configuration
    .AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.json"), optional: true, reloadOnChange: false)
    .AddJsonFile(Path.Combine(AppContext.BaseDirectory, $"appsettings.{builder.Environment.EnvironmentName}.json"), optional: true, reloadOnChange: false);
builder.Services.AddDbContext<GameDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("GameDatabase") ?? "Data Source=immortal-loot-dev.db"));
builder.Services.AddSingleton<IServerClock, ServerClock>();
builder.Services.AddSingleton(_ => ServerGameConfigCatalog.LoadDefault());
builder.Services.AddScoped<BattleAuthorityService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<PlayerQueryService>();
builder.Services.AddScoped<CurrencyService>();
builder.Services.AddScoped<ShopService>();
if (builder.Environment.IsDevelopment() && builder.Configuration.GetValue<bool>("Payments:EnableMockProvider"))
    builder.Services.AddSingleton<IPaymentReceiptVerifier, DevelopmentPaymentReceiptVerifier>();
else
    builder.Services.AddSingleton<IPaymentReceiptVerifier, RejectingPaymentReceiptVerifier>();
builder.Services.AddScoped<PaymentService>();
builder.Services.AddSingleton<IRankingCache, MemoryRankingCache>();
builder.Services.AddScoped<RankingService>();
builder.Services.AddScoped<RewardService>();
builder.Services.AddScoped<TaskService>();
builder.Services.AddScoped<MailService>();
builder.Services.AddSingleton<ActivityService>();
builder.Services.AddScoped<EquipmentAuthorityService>();
builder.Services.AddScoped<AfkAuthorityService>();
builder.Services.AddScoped<RealmAuthorityService>();
builder.Services.AddSingleton<IServerRandomSource, CryptoServerRandomSource>();
builder.Services.AddScoped<ServerEquipmentDropService>();

var app = builder.Build();
await using (var scope = app.Services.CreateAsyncScope())
{
    var services = scope.ServiceProvider;
    await GameDatabaseInitializer.InitializeAsync(
        services.GetRequiredService<GameDbContext>(),
        services.GetRequiredService<IServerClock>().UtcNow);
}

app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "ImmortalLoot.Server" }));

app.MapPost("/auth/login", async (LoginRequest request, AuthService service, CancellationToken cancellationToken) =>
{
    try { return Results.Ok(await service.LoginAsync(request.Provider, request.ExternalAccountId, request.Nickname, cancellationToken)); }
    catch (ArgumentException exception) { return Results.BadRequest(new { error = exception.Message }); }
    catch (InvalidOperationException exception) { return Results.Conflict(new { error = exception.Message }); }
});

app.MapGet("/player/profile", async (HttpRequest request, AuthService auth, PlayerQueryService players, CancellationToken cancellationToken) =>
{
    var playerId = await auth.ResolvePlayerAsync(request.Headers.Authorization, cancellationToken);
    return playerId.HasValue ? Results.Ok(await players.GetProfileAsync(playerId.Value, cancellationToken)) : Results.Unauthorized();
});

app.MapGet("/player/inventory", async (HttpRequest request, AuthService auth, PlayerQueryService players, CancellationToken cancellationToken) =>
{
    var playerId = await auth.ResolvePlayerAsync(request.Headers.Authorization, cancellationToken);
    return playerId.HasValue ? Results.Ok(await players.GetInventoryAsync(playerId.Value, cancellationToken)) : Results.Unauthorized();
});

app.MapGet("/shop/offers", (ShopService shop) => Results.Ok(shop.List()));
app.MapGet("/shop", (ShopService shop) => Results.Ok(shop.List()));

app.MapPost("/shop/purchase", async (ShopPurchaseRequest request, HttpRequest httpRequest, AuthService auth, ShopService shop, CancellationToken cancellationToken) =>
{
    try
    {
        var playerId = await auth.ResolvePlayerAsync(httpRequest.Headers.Authorization, cancellationToken);
        if (!playerId.HasValue) return Results.Unauthorized();
        return Results.Ok(await shop.PurchaseAsync(playerId.Value, request.ProductId, request.Quantity, request.IdempotencyKey, cancellationToken));
    }
    catch (KeyNotFoundException exception) { return Results.NotFound(new { error = exception.Message }); }
    catch (ArgumentException exception) { return Results.BadRequest(new { error = exception.Message }); }
    catch (InvalidOperationException exception) { return Results.Conflict(new { error = exception.Message }); }
});

app.MapPost("/shop/buy", async (ShopPurchaseRequest body, HttpRequest request, AuthService auth, ShopService shop, CancellationToken cancellationToken) =>
{
    try
    {
        var playerId = await auth.ResolvePlayerAsync(request.Headers.Authorization, cancellationToken);
        if (!playerId.HasValue) return Results.Unauthorized();
        return Results.Ok(await shop.PurchaseAsync(playerId.Value, body.ProductId, body.Quantity, body.IdempotencyKey, cancellationToken));
    }
    catch (KeyNotFoundException exception) { return Results.NotFound(new { error = exception.Message }); }
    catch (ArgumentException exception) { return Results.BadRequest(new { error = exception.Message }); }
    catch (InvalidOperationException exception) { return Results.Conflict(new { error = exception.Message }); }
});

app.MapGet("/payment/products", (PaymentService payments) => Results.Ok(payments.ListProducts()));

app.MapGet("/payment/entitlements", async (HttpRequest request, AuthService auth, PaymentService payments, CancellationToken cancellationToken) =>
{
    var playerId = await auth.ResolvePlayerAsync(request.Headers.Authorization, cancellationToken);
    return playerId.HasValue ? Results.Ok(await payments.GetEntitlementsAsync(playerId.Value, cancellationToken)) : Results.Unauthorized();
});

app.MapPost("/payment/daily-claim", async (HttpRequest request, AuthService auth, PaymentService payments, CancellationToken cancellationToken) =>
{
    try
    {
        var playerId = await auth.ResolvePlayerAsync(request.Headers.Authorization, cancellationToken);
        if (!playerId.HasValue) return Results.Unauthorized();
        return Results.Ok(await payments.ClaimDailyEntitlementsAsync(playerId.Value, cancellationToken));
    }
    catch (InvalidOperationException exception) { return Results.Conflict(new { error = exception.Message }); }
});

app.MapPost("/payment/orders", async (PaymentOrderRequest request, HttpRequest httpRequest, AuthService auth, PaymentService payments, CancellationToken cancellationToken) =>
{
    try
    {
        var playerId = await auth.ResolvePlayerAsync(httpRequest.Headers.Authorization, cancellationToken);
        if (!playerId.HasValue) return Results.Unauthorized();
        return Results.Ok(await payments.CreateOrderAsync(playerId.Value, request.ProductId, cancellationToken));
    }
    catch (KeyNotFoundException exception) { return Results.NotFound(new { error = exception.Message }); }
});

app.MapPost("/payment/create-order", async (PaymentOrderRequest body, HttpRequest request, AuthService auth, PaymentService payments, CancellationToken cancellationToken) =>
{
    try
    {
        var playerId = await auth.ResolvePlayerAsync(request.Headers.Authorization, cancellationToken);
        if (!playerId.HasValue) return Results.Unauthorized();
        return Results.Ok(await payments.CreateOrderAsync(playerId.Value, body.ProductId, cancellationToken));
    }
    catch (KeyNotFoundException exception) { return Results.NotFound(new { error = exception.Message }); }
});

app.MapPost("/payment/verify", async (PaymentVerifyRequest request, HttpRequest httpRequest, AuthService auth, PaymentService payments, CancellationToken cancellationToken) =>
{
    try
    {
        var playerId = await auth.ResolvePlayerAsync(httpRequest.Headers.Authorization, cancellationToken);
        if (!playerId.HasValue) return Results.Unauthorized();
        return Results.Ok(await payments.VerifyAndGrantAsync(playerId.Value, request.OrderNo, request.Provider, request.Receipt, cancellationToken));
    }
    catch (KeyNotFoundException exception) { return Results.NotFound(new { error = exception.Message }); }
    catch (ArgumentException exception) { return Results.BadRequest(new { error = exception.Message }); }
    catch (InvalidOperationException exception) { return Results.Conflict(new { error = exception.Message }); }
});

app.MapGet("/rankings/{type}", async (string type, string? period, int? page, int? pageSize, HttpRequest request, AuthService auth, RankingService rankings, CancellationToken cancellationToken) =>
{
    if (!Enum.TryParse<RankingType>(type, true, out var rankingType)) return Results.BadRequest(new { error = "Ranking type must be Power, Realm, or Stage." });
    var playerId = await auth.ResolvePlayerAsync(request.Headers.Authorization, cancellationToken);
    try { return Results.Ok(await rankings.GetPageAsync(rankingType, period, page ?? 1, pageSize ?? 20, playerId, cancellationToken)); }
    catch (ArgumentOutOfRangeException exception) { return Results.BadRequest(new { error = exception.Message }); }
});

app.MapGet("/ranking", async (string? type, string? period, int? page, int? pageSize, HttpRequest request, AuthService auth, RankingService rankings, CancellationToken cancellationToken) =>
{
    if (!Enum.TryParse<RankingType>(type ?? "Power", true, out var rankingType)) return Results.BadRequest(new { error = "Ranking type must be Power, Realm, or Stage." });
    var playerId = await auth.ResolvePlayerAsync(request.Headers.Authorization, cancellationToken);
    try { return Results.Ok(await rankings.GetPageAsync(rankingType, period, page ?? 1, pageSize ?? 20, playerId, cancellationToken)); }
    catch (ArgumentOutOfRangeException exception) { return Results.BadRequest(new { error = exception.Message }); }
});

app.MapGet("/activities", (ActivityService activities) => Results.Ok(activities.ListActive()));

app.MapPost("/equipment/equip", async (EquipmentEquipRequest body, HttpRequest request, AuthService auth, EquipmentAuthorityService equipment, CancellationToken cancellationToken) =>
{
    try
    {
        var playerId = await auth.ResolvePlayerAsync(request.Headers.Authorization, cancellationToken);
        if (!playerId.HasValue) return Results.Unauthorized();
        return Results.Ok(await equipment.EquipAsync(playerId.Value, body.InstanceId, cancellationToken));
    }
    catch (KeyNotFoundException exception) { return Results.NotFound(new { error = exception.Message }); }
});

app.MapPost("/equipment/decompose", async (EquipmentDecomposeRequest body, HttpRequest request, AuthService auth, EquipmentAuthorityService equipment, CancellationToken cancellationToken) =>
{
    try
    {
        var playerId = await auth.ResolvePlayerAsync(request.Headers.Authorization, cancellationToken);
        if (!playerId.HasValue) return Results.Unauthorized();
        return Results.Ok(await equipment.DecomposeAsync(playerId.Value, body.InstanceId, body.IdempotencyKey, cancellationToken));
    }
    catch (KeyNotFoundException exception) { return Results.NotFound(new { error = exception.Message }); }
    catch (ArgumentException exception) { return Results.BadRequest(new { error = exception.Message }); }
    catch (InvalidOperationException exception) { return Results.Conflict(new { error = exception.Message }); }
});

app.MapPost("/equipment/enhance", async (EquipmentDecomposeRequest body, HttpRequest request, AuthService auth, EquipmentAuthorityService equipment, CancellationToken cancellationToken) =>
{
    try
    {
        var playerId = await auth.ResolvePlayerAsync(request.Headers.Authorization, cancellationToken);
        if (!playerId.HasValue) return Results.Unauthorized();
        return Results.Ok(await equipment.EnhanceAsync(playerId.Value, body.InstanceId, body.IdempotencyKey, cancellationToken));
    }
    catch (KeyNotFoundException exception) { return Results.NotFound(new { error = exception.Message }); }
    catch (ArgumentException exception) { return Results.BadRequest(new { error = exception.Message }); }
    catch (InvalidOperationException exception) { return Results.Conflict(new { error = exception.Message }); }
});

app.MapGet("/afk/reward", async (HttpRequest request, AuthService auth, AfkAuthorityService afk, CancellationToken cancellationToken) =>
{
    var playerId = await auth.ResolvePlayerAsync(request.Headers.Authorization, cancellationToken);
    return playerId.HasValue ? Results.Ok(await afk.PreviewAsync(playerId.Value, cancellationToken)) : Results.Unauthorized();
});

app.MapPost("/afk/claim", async (IdempotencyRequest body, HttpRequest request, AuthService auth, AfkAuthorityService afk, CancellationToken cancellationToken) =>
{
    try
    {
        var playerId = await auth.ResolvePlayerAsync(request.Headers.Authorization, cancellationToken);
        if (!playerId.HasValue) return Results.Unauthorized();
        return Results.Ok(await afk.ClaimAsync(playerId.Value, body.IdempotencyKey, cancellationToken));
    }
    catch (ArgumentException exception) { return Results.BadRequest(new { error = exception.Message }); }
});

app.MapPost("/afk/quick-claim", async (IdempotencyRequest body, HttpRequest request, AuthService auth, AfkAuthorityService afk, CancellationToken cancellationToken) =>
{
    try
    {
        var playerId = await auth.ResolvePlayerAsync(request.Headers.Authorization, cancellationToken);
        if (!playerId.HasValue) return Results.Unauthorized();
        return Results.Ok(await afk.ClaimQuickAsync(playerId.Value, body.IdempotencyKey, cancellationToken));
    }
    catch (KeyNotFoundException exception) { return Results.NotFound(new { error = exception.Message }); }
    catch (ArgumentException exception) { return Results.BadRequest(new { error = exception.Message }); }
    catch (InvalidOperationException exception) { return Results.Conflict(new { error = exception.Message }); }
});

app.MapPost("/realm/breakthrough", async (IdempotencyRequest body, HttpRequest request, AuthService auth, RealmAuthorityService realms, CancellationToken cancellationToken) =>
{
    try
    {
        var playerId = await auth.ResolvePlayerAsync(request.Headers.Authorization, cancellationToken);
        if (!playerId.HasValue) return Results.Unauthorized();
        return Results.Ok(await realms.BreakthroughAsync(playerId.Value, body.IdempotencyKey, cancellationToken));
    }
    catch (KeyNotFoundException exception) { return Results.NotFound(new { error = exception.Message }); }
    catch (ArgumentException exception) { return Results.BadRequest(new { error = exception.Message }); }
    catch (InvalidOperationException exception) { return Results.Conflict(new { error = exception.Message }); }
});

app.MapGet("/tasks", async (HttpRequest request, AuthService auth, TaskService tasks, CancellationToken cancellationToken) =>
{
    var playerId = await auth.ResolvePlayerAsync(request.Headers.Authorization, cancellationToken);
    return playerId.HasValue ? Results.Ok(await tasks.ListAsync(playerId.Value, cancellationToken)) : Results.Unauthorized();
});

app.MapPost("/tasks/{taskId}/claim", async (string taskId, HttpRequest request, AuthService auth, TaskService tasks, CancellationToken cancellationToken) =>
{
    try
    {
        var playerId = await auth.ResolvePlayerAsync(request.Headers.Authorization, cancellationToken);
        if (!playerId.HasValue) return Results.Unauthorized();
        return Results.Ok(await tasks.ClaimAsync(playerId.Value, taskId, cancellationToken));
    }
    catch (KeyNotFoundException exception) { return Results.NotFound(new { error = exception.Message }); }
    catch (InvalidOperationException exception) { return Results.Conflict(new { error = exception.Message }); }
});

app.MapPost("/tasks/activity/{requiredPoints:int}/claim", async (int requiredPoints, HttpRequest request, AuthService auth, TaskService tasks, CancellationToken cancellationToken) =>
{
    try
    {
        var playerId = await auth.ResolvePlayerAsync(request.Headers.Authorization, cancellationToken);
        if (!playerId.HasValue) return Results.Unauthorized();
        return Results.Ok(await tasks.ClaimActivityChestAsync(playerId.Value, requiredPoints, cancellationToken));
    }
    catch (KeyNotFoundException exception) { return Results.NotFound(new { error = exception.Message }); }
    catch (InvalidOperationException exception) { return Results.Conflict(new { error = exception.Message }); }
});

app.MapGet("/mail", async (HttpRequest request, AuthService auth, MailService mail, CancellationToken cancellationToken) =>
{
    var playerId = await auth.ResolvePlayerAsync(request.Headers.Authorization, cancellationToken);
    return playerId.HasValue ? Results.Ok(await mail.ListAsync(playerId.Value, cancellationToken)) : Results.Unauthorized();
});

app.MapPost("/mail/{mailId:guid}/claim", async (Guid mailId, HttpRequest request, AuthService auth, MailService mail, CancellationToken cancellationToken) =>
{
    try
    {
        var playerId = await auth.ResolvePlayerAsync(request.Headers.Authorization, cancellationToken);
        if (!playerId.HasValue) return Results.Unauthorized();
        return Results.Ok(await mail.ClaimAsync(playerId.Value, mailId, cancellationToken));
    }
    catch (KeyNotFoundException exception) { return Results.NotFound(new { error = exception.Message }); }
    catch (InvalidOperationException exception) { return Results.Conflict(new { error = exception.Message }); }
});

app.MapPost("/mail/claim", async (MailClaimRequest body, HttpRequest request, AuthService auth, MailService mail, CancellationToken cancellationToken) =>
{
    try
    {
        var playerId = await auth.ResolvePlayerAsync(request.Headers.Authorization, cancellationToken);
        if (!playerId.HasValue) return Results.Unauthorized();
        return Results.Ok(await mail.ClaimAsync(playerId.Value, body.MailId, cancellationToken));
    }
    catch (KeyNotFoundException exception) { return Results.NotFound(new { error = exception.Message }); }
    catch (InvalidOperationException exception) { return Results.Conflict(new { error = exception.Message }); }
});

app.MapPost("/battle/start", async (BattleStartRequest request, HttpRequest httpRequest, AuthService auth, BattleAuthorityService service, CancellationToken cancellationToken) =>
{
    try
    {
        var playerId = await auth.ResolvePlayerAsync(httpRequest.Headers.Authorization, cancellationToken);
        if (!playerId.HasValue) return Results.Unauthorized();
        return Results.Ok(await service.StartAsync(playerId.Value, request.StageId, request.IdempotencyKey, cancellationToken));
    }
    catch (KeyNotFoundException exception) { return Results.NotFound(new { error = exception.Message }); }
    catch (ArgumentException exception) { return Results.BadRequest(new { error = exception.Message }); }
    catch (InvalidOperationException exception) { return Results.Conflict(new { error = exception.Message }); }
});

app.MapPost("/battle/finish", async (BattleFinishRequest request, HttpRequest httpRequest, AuthService auth, BattleAuthorityService service, CancellationToken cancellationToken) =>
{
    try
    {
        var playerId = await auth.ResolvePlayerAsync(httpRequest.Headers.Authorization, cancellationToken);
        if (!playerId.HasValue) return Results.Unauthorized();
        return Results.Ok(await service.FinishAsync(playerId.Value, request.SessionId, request.IdempotencyKey, request.RewardWindowEligible ?? false, cancellationToken));
    }
    catch (KeyNotFoundException exception) { return Results.NotFound(new { error = exception.Message }); }
    catch (ArgumentException exception) { return Results.BadRequest(new { error = exception.Message }); }
});

app.Run();

public sealed record LoginRequest(string Provider, string ExternalAccountId, string Nickname);
public sealed record BattleStartRequest(string StageId, string IdempotencyKey);
public sealed record BattleFinishRequest(Guid SessionId, string IdempotencyKey, bool? RewardWindowEligible = null);
public sealed record ShopPurchaseRequest(string ProductId, int Quantity, string IdempotencyKey);
public sealed record PaymentOrderRequest(string ProductId);
public sealed record PaymentVerifyRequest(string OrderNo, string Provider, string Receipt);
public sealed record EquipmentEquipRequest(string InstanceId);
public sealed record EquipmentDecomposeRequest(string InstanceId, string IdempotencyKey);
public sealed record IdempotencyRequest(string IdempotencyKey);
public sealed record MailClaimRequest(Guid MailId);

public partial class Program;
