using Azure.Storage.Blobs;
using DotNetEnv;
using PawPoint.ApiServices.Exceptions;
using PawPoint.ApiServices.Helpers;
using PawPoint.ApiServices.Hubs;
using PawPoint.Common.Helpers;
using PawPoint.DB;
using PawPoint.DB.Entities;
using PawPoint.Services.Interfaces;
using PawPoint.Services.Jobs;
using PawPoint.Services.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.Extensions.Hosting;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

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
                policy.WithOrigins("https://lateral-inspire.vercel.app/")
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

        builder.AddNpgsqlDbContext<Context>(connectionName: "PawPointDB");

        var cs = builder.Configuration.GetConnectionString("profile-pics");
        var dataUpdateCs = builder.Configuration.GetConnectionString("data-updates");

        builder.Services.AddSingleton(_ => new BlobServiceClient(cs));
        builder.Services.AddSingleton(_ => new BlobServiceClient(dataUpdateCs));

        builder.Services.AddKeyedSingleton("data-updates", (sp, _) =>
        {
            var client = sp.GetRequiredService<BlobServiceClient>();
            return client.GetBlobContainerClient("data-updates");
        });

        builder.Services.AddKeyedSingleton("profile-pics", (sp, _) =>
        {
            var client = sp.GetRequiredService<BlobServiceClient>();
            return client.GetBlobContainerClient("profile-pics");
        });

        var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";

        Env.Load(Path.Combine(Directory.GetCurrentDirectory(), $"env.env"));

        builder.Configuration
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddJsonFile($"appsettings.{env}.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables();

        builder.Services.Configure<AppUrls>(
            builder.Configuration.GetSection("AppUrls"));

        builder.Services.Configure<StorageSettings>(
            builder.Configuration.GetSection("Storage"));

        builder.Services.Configure<VerificationTokenSettings>(
            builder.Configuration.GetSection("VerificationTokens"));

        builder.Services.Configure<JwtSettings>(
            builder.Configuration.GetSection("Jwt"));

        builder.Services.Configure<EncryptionSettings>(
            builder.Configuration.GetSection("Encryption"));

        builder.Services.Configure<TemplateSettings>(
            builder.Configuration.GetSection("Templates"));

        builder.Services.Configure<MailJetSettings>(
            builder.Configuration.GetSection("MailJet"));

        builder.Services.AddHttpClient("Mailjet", (sp, client) =>
        {
            var settings = sp.GetRequiredService<IOptions<MailJetSettings>>().Value;
            client.BaseAddress = new Uri(settings.Endpoint);

            var byteArray = Encoding.ASCII.GetBytes($"{settings.Key}:{settings.Secret}");
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));
        });

        builder.Services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            var jwtSection = builder.Configuration.GetSection("Jwt");
            var jwt = jwtSection.Get<JwtSettings>()!;
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
                    return System.Threading.Tasks.Task.CompletedTask;
                }
            };
        });

        builder.Services.AddScoped<IDatabaseSeedService, DatabaseSeedService>();
        builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
        builder.Services.AddScoped<IPasswordService, PasswordService>();
        builder.Services.AddScoped<IAuthService, AuthService>();
        builder.Services.AddScoped<ITokenService, TokenService>();;
        builder.Services.AddScoped<IIdentityService, IdentityService>();
        builder.Services.AddScoped<IProfilePictureService, ProfilePictureService>();
        builder.Services.AddScoped<IProfilePictureUrlFactory, ProfilePictureUrlFactory>();
        builder.Services.AddScoped<IUserService, UserService>();
        builder.Services.AddScoped<INotificationService, NotificationService>();;
        builder.Services.AddScoped<IAnimalService, AnimalService>();

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

        builder.Services.AddHangfire(cfg =>
        {
            var conn = builder.Configuration.GetConnectionString("PawPointDB");
            cfg.UseSimpleAssemblyNameTypeSerializer()
               .UseRecommendedSerializerSettings()
               .UsePostgreSqlStorage(conn);
        });
        builder.Services.AddHangfireServer();

        var app = builder.Build();
        app.UseExceptionHandler();

        app.MapDefaultEndpoints();

        if (app.Environment.IsDevelopment())
        {
            app.UseCors("AllowDevTools");
        }
        else
        {
            app.UseCors("AllowFrontendApp");
        }

        if (app.Environment.IsDevelopment() || app.Environment.IsStaging())
        {
            app.MapOpenApi();
            app.MapScalarApiReference();

            using var scope = app.Services.CreateScope();
            var seedService = scope.ServiceProvider.GetRequiredService<IDatabaseSeedService>();
            seedService.MigrateDatabase(scope);
        }

        app.UseHttpsRedirection();
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
            new RecurringJobOptions{TimeZone = TimeZoneInfo.Utc}
        );
        RecurringJob.AddOrUpdate<DataUpdateProcessingJob>(
        "process-all-unprocessed-files",
        job => job.RunAsync(),
        Cron.Daily(3)
        );

        app.Run();
    }
}
