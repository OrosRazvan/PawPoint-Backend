using Azure.Storage.Blobs;
using DotNetEnv;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using PawPoint.ApiServices.Exceptions;
using PawPoint.ApiServices.Helpers;
using PawPoint.ApiServices.Hubs;
using PawPoint.Common.Helpers;
using PawPoint.DB;
using PawPoint.DB.Entities;
using PawPoint.Services.Interfaces;
using PawPoint.Services.Jobs;
using PawPoint.Services.Services;
using Scalar.AspNetCore;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // =========================
        // 1) CONFIG LOADING FIRST
        // =========================
        var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";

        // load .env (dacă îl folosești)
        Env.Load(Path.Combine(Directory.GetCurrentDirectory(), "env.env"));

        // IMPORTANT: încarcă json + env vars înainte să citești connection strings
        builder.Configuration
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddJsonFile($"appsettings.{env}.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables();

        builder.AddServiceDefaults();

        builder.Services.AddControllers();
        builder.Services.AddOpenApi();
        builder.Services.AddControllersWithViews();
        builder.Services.AddRazorTemplating();

        builder.Services.AddSignalR().AddJsonProtocol();

        builder.Services.AddCors(options =>
        {
            options.AddPolicy("AllowFrontendApp", policy =>
            {
                policy.WithOrigins("https://lateral-inspire.vercel.app")
                      .AllowAnyHeader()
                      .AllowAnyMethod();
            });

            options.AddPolicy("AllowDevTools", policy =>
            {
                policy.WithOrigins(
                    "https://localhost:5001",
                    "http://localhost:5000",
                    "https://scalar.local",
                    "http://127.0.0.1:5173")
                      .AllowAnyHeader()
                      .AllowAnyMethod();
            });
        });

        // =========================
        // 2) DATABASE (ONE CS)
        // =========================
        // Folosește o singură cheie pentru DB peste tot
        // (schimbă numele dacă vrei, dar să fie același și la Hangfire)
        var dbConn = builder.Configuration.GetConnectionString("PawPointDB")
                    ?? builder.Configuration.GetConnectionString("DefaultConnection");

        if (string.IsNullOrWhiteSpace(dbConn))
            throw new InvalidOperationException("Lipsește ConnectionStrings:PawPointDB (sau DefaultConnection).");

        builder.Services.AddDbContext<Context>(options =>
            options.UseNpgsql(dbConn, x => x.MigrationsAssembly("PawPoint.DB")));

        // =========================
        // 3) AZURE BLOBS (KEYED)
        // =========================
        var profilePicsConn = builder.Configuration.GetConnectionString("profile-pics");
        var dataUpdatesConn = builder.Configuration.GetConnectionString("data-updates");

        if (string.IsNullOrWhiteSpace(profilePicsConn))
            throw new InvalidOperationException("Lipsește ConnectionStrings:profile-pics.");

        if (string.IsNullOrWhiteSpace(dataUpdatesConn))
            throw new InvalidOperationException("Lipsește ConnectionStrings:data-updates.");

        // doi clienți separați, keyed
        builder.Services.AddKeyedSingleton("profile-pics-client", (_, __) => new BlobServiceClient(profilePicsConn));
        builder.Services.AddKeyedSingleton("data-updates-client", (_, __) => new BlobServiceClient(dataUpdatesConn));

        builder.Services.AddKeyedSingleton("profile-pics", (sp, _) =>
        {
            var client = sp.GetRequiredKeyedService<BlobServiceClient>("profile-pics-client");
            return client.GetBlobContainerClient("profile-pics");
        });

        builder.Services.AddKeyedSingleton("data-updates", (sp, _) =>
        {
            var client = sp.GetRequiredKeyedService<BlobServiceClient>("data-updates-client");
            return client.GetBlobContainerClient("data-updates");
        });

        // =========================
        // 4) OPTIONS / SETTINGS
        // =========================
        builder.Services.Configure<AppUrls>(builder.Configuration.GetSection("AppUrls"));
        builder.Services.Configure<StorageSettings>(builder.Configuration.GetSection("Storage"));
        builder.Services.Configure<VerificationTokenSettings>(builder.Configuration.GetSection("VerificationTokens"));
        builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("Jwt"));
        builder.Services.Configure<EncryptionSettings>(builder.Configuration.GetSection("Encryption"));
        builder.Services.Configure<TemplateSettings>(builder.Configuration.GetSection("Templates"));
        builder.Services.Configure<MailJetSettings>(builder.Configuration.GetSection("MailJet"));

        builder.Services.AddHttpClient("Mailjet", (sp, client) =>
        {
            var settings = sp.GetRequiredService<IOptions<MailJetSettings>>().Value;
            client.BaseAddress = new Uri(settings.Endpoint);

            var byteArray = Encoding.ASCII.GetBytes($"{settings.Key}:{settings.Secret}");
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));
        });

        // =========================
        // 5) AUTH
        // =========================
        builder.Services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            var jwt = builder.Configuration.GetSection("Jwt").Get<JwtSettings>()
                      ?? throw new InvalidOperationException("Secțiunea Jwt lipsește din config.");

            options.RequireHttpsMetadata = true;
            options.SaveToken = false;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.AccessSecret)),
                ValidateIssuer = true,
                ValidIssuer = jwt.Issuer,
                ValidateAudience = true,
                ValidAudience = jwt.Audience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero,
                NameClaimType = ClaimTypes.NameIdentifier
            };

            options.Events = new JwtBearerEvents
            {
                OnMessageReceived = context =>
                {
                    var accessToken = context.Request.Query["access_token"];
                    var path = context.HttpContext.Request.Path;

                    if (!string.IsNullOrEmpty(accessToken) &&
                        path.StartsWithSegments("/hubs/notifications"))
                    {
                        context.Token = accessToken;
                    }

                    return Task.CompletedTask;
                }
            };
        });

        // =========================
        // 6) DI
        // =========================
        builder.Services.AddScoped<IDatabaseSeedService, DatabaseSeedService>();
        builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
        builder.Services.AddScoped<IPasswordService, PasswordService>();
        builder.Services.AddScoped<IAuthService, AuthService>();
        builder.Services.AddScoped<ITokenService, TokenService>();
        builder.Services.AddScoped<IIdentityService, IdentityService>();
        builder.Services.AddScoped<IProfilePictureService, ProfilePictureService>();
        builder.Services.AddScoped<IProfilePictureUrlFactory, ProfilePictureUrlFactory>();
        builder.Services.AddScoped<IUserService, UserService>();
        builder.Services.AddScoped<INotificationService, NotificationService>();
        builder.Services.AddScoped<IAnimalService, AnimalService>();
        builder.Services.AddScoped<IAppointmentService, AppointmentService>();
        builder.Services.AddScoped<IVaccinationService, VaccinationService>();
        builder.Services.AddScoped<IDewormingService, DewormingService>();
        builder.Services.AddScoped<IFeedingService, FeedingService>();

        builder.Services.AddSingleton<IHubContext<Hub>>(sp =>
            (IHubContext<Hub>)sp.GetRequiredService<IHubContext<NotificationHub>>());
        builder.Services.AddSingleton<INotificationRealtimeDispatcher, NotificationRealtimeDispatcher>();

        builder.Services.AddScoped<DailyNotificationJob>();
        builder.Services.AddScoped<ExpiredVerificationTokenCleanupJob>();

        builder.Services.AddProblemDetails();
        builder.Services.AddExceptionHandler<ExceptionHandlingMiddleware>();

        builder.Services.AddSingleton<IPiiEncryptionService, PiiEncryptionService>();
        builder.Services.AddSingleton<IEmailIndexService, EmailIndexService>();
        builder.Services.AddSingleton<IEmailService, EmailService>();
        builder.Services.AddSingleton<ITemplateRenderer, TemplateRenderer>();

        // =========================
        // 7) HANGFIRE (same DB CS)
        // =========================
        builder.Services.AddHangfire(cfg =>
        {
            cfg.UseSimpleAssemblyNameTypeSerializer()
               .UseRecommendedSerializerSettings()
               .UsePostgreSqlStorage(dbConn);
        });
        builder.Services.AddHangfireServer();

        var app = builder.Build();

        app.UseExceptionHandler();
        app.MapDefaultEndpoints();

        if (app.Environment.IsDevelopment())
            app.UseCors("AllowDevTools");
        else
            app.UseCors("AllowFrontendApp");

        // =========================
        // 8) MIGRATE + SEED (startup)
        // =========================
        // Dacă vrei strict doar Dev/Staging, pune if în jur.
        using (var scope = app.Services.CreateScope())
        {
            var seedService = scope.ServiceProvider.GetRequiredService<IDatabaseSeedService>();
            seedService.MigrateDatabase(scope);
        }

        if (app.Environment.IsDevelopment() || app.Environment.IsStaging())
        {
            app.MapOpenApi();
            app.MapScalarApiReference();
        }

        app.UseHttpsRedirection();
        app.UseAuthentication();
        app.UseAuthorization();

        app.MapControllers();
        app.MapHub<NotificationHub>("/hubs/notifications");

        app.UseHangfireDashboard("/hangfire");

        RecurringJob.AddOrUpdate<DailyNotificationJob>(
            "daily-notifications",
            job => job.Run(),
            Cron.Daily(7)
        );

        RecurringJob.AddOrUpdate<ExpiredVerificationTokenCleanupJob>(
            "expired-verification-tokens",
            job => job.RunAsync(),
            Cron.Daily(3),
            new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc }
        );

        RecurringJob.AddOrUpdate<DataUpdateProcessingJob>(
            "process-all-unprocessed-files",
            job => job.RunAsync(),
            Cron.Daily(3)
        );

        app.Run();
    }
}
