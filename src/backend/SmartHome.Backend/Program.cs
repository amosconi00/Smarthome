using Microsoft.EntityFrameworkCore;
using SmartHome.Backend.Dal;
using AutoMapper;
using SmartHome.Backend.Hubs;
using SmartHome.Backend.BackgroundServices; // updated
using SmartHome.Backend.Notifiers; // updated
using SmartHome.Backend.Services;
using SmartHome.Backend.Settings; // added

namespace SmartHome.Backend
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.

            builder.Services.AddControllers();
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();
            builder.Services.AddSignalR();

            // Bind NILM settings
            builder.Services.Configure<NilmSettings>(builder.Configuration.GetSection("NILM"));
            builder.Services.Configure<MqttSettings>(builder.Configuration.GetSection("MQTT")); // added
            builder.Services.AddSingleton<INilmModeService, NilmModeService>();

            builder.Services.AddHostedService<MqttClientService>();
            builder.Services.AddHostedService<RealtimeTelemetryBroadcastService>();
            builder.Services.AddHostedService<NilmJobBackgroundService>(); // sempre registrato ma pu? essere disattivato logicamente

            // Configure EF Core DbContextFactory with PostgreSQL
            builder.Services.AddDbContextFactory<SmartHomeDbContext>(options =>
            {
                var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
                options.UseNpgsql(connectionString, npg =>
                {
                    // 0 = infinito (nessun timeout lato client sulle command)
                    npg.CommandTimeout(99999);
                });
                options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
                options.EnableDetailedErrors();
                options.EnableSensitiveDataLogging();
        });

            //add automigration
            using (var scope = builder.Services.BuildServiceProvider().CreateScope())
            {
                var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<SmartHomeDbContext>>();
                using (var dbContext = dbContextFactory.CreateDbContext())
                {
                    dbContext.Database.Migrate();
                }
            }

            // Register AutoMapper and scan for profiles in the current domain assemblies
            builder.Services.AddAutoMapper(AppDomain.CurrentDomain.GetAssemblies());

            // CORS: allow all origins (use only in development/testing)
            const string CorsPolicyName = "FrontendCors";
            builder.Services.AddCors(options =>
            {
                options.AddPolicy(CorsPolicyName, policy =>
                {
                    policy
                        .AllowAnyOrigin()
                        .AllowAnyHeader()
                        .AllowAnyMethod();
                    // Do NOT enable AllowCredentials with AllowAnyOrigin
                });
            });

            // Register application services
            PrepareDI(builder);
            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            //app.UseHttpsRedirection();

            // Apply CORS before authorization and endpoints
            app.UseCors(CorsPolicyName);

            app.UseAuthorization();


            app.MapControllers();
            app.MapHub<TelemetryHub>("/hubs/telemetry");

            app.Run();
        }

        private static void PrepareDI(WebApplicationBuilder builder)
        {
            builder.Services.AddScoped<SmartHome.Backend.Services.IApplianceService, SmartHome.Backend.Services.ApplianceService>();
            builder.Services.AddScoped<SmartHome.Backend.Services.IPlugService, SmartHome.Backend.Services.PlugService>();
            builder.Services.AddScoped<SmartHome.Backend.Services.ICategoryApplianceService, SmartHome.Backend.Services.CategoryApplianceService>();
            builder.Services.AddScoped<SmartHome.Backend.Services.IPowerMeterService, SmartHome.Backend.Services.PowerMeterService>();
            builder.Services.AddScoped<SmartHome.Backend.Services.IApplianceSignatureService, SmartHome.Backend.Services.ApplianceSignatureService>();
            builder.Services.AddScoped<SmartHome.Backend.Services.ICategoryApplianceSignatureService, SmartHome.Backend.Services.CategoryApplianceSignatureService>();
            builder.Services.AddScoped<SmartHome.Backend.Services.IApplianceSignatureGenerationService, SmartHome.Backend.Services.ApplianceSignatureGenerationService>();
            builder.Services.AddScoped<SmartHome.Backend.Services.IExportService, SmartHome.Backend.Services.ExportService>();
            builder.Services.AddSingleton<SmartHome.Backend.Services.INilmHartService, SmartHome.Backend.Services.NilmHartService>();
            builder.Services.AddScoped<SmartHome.Backend.Services.INilmService, SmartHome.Backend.Services.NilmService>();
            builder.Services.AddSingleton<SmartHome.Backend.Services.IPipelineTelemetryService, SmartHome.Backend.Services.PipelineTelemetryService>(); // fix BackEnd typo
            builder.Services.AddScoped<SmartHome.Backend.Services.ITelemetryService, SmartHome.Backend.Services.TelemetryService>();
            builder.Services.AddScoped<SmartHome.Backend.Services.IElectricityTariffService, SmartHome.Backend.Services.ElectricityTariffService>();
            builder.Services.AddSingleton<SmartHome.Backend.Notifiers.IPlugChangeNotifier, SmartHome.Backend.Notifiers.PlugChangeNotifier>();
            builder.Services.AddSingleton<SmartHome.Backend.Notifiers.IPowerMeterChangeNotifier, SmartHome.Backend.Notifiers.PowerMeterChangeNotifier>();
            builder.Services.AddSingleton<SmartHome.Backend.Notifiers.IApplianceSignatureChangeNotifier, SmartHome.Backend.Notifiers.ApplianceSignatureChangeNotifier>();
            //builder.Services.AddScoped<SmartHome.Backend.Services.INilmBatchService, SmartHome.Backend.Services.NilmBatchService>();
        }
    }
}
