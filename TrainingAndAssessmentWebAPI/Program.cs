using Microsoft.EntityFrameworkCore;
using TrainingAndAssessmentDataAccessLayer;
using TrainingAndAssessmentWebAPI.Services;
using TrainingAndAssessmentWebAPI.Hubs;

namespace TrainingAndAssessmentWebAPI
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services.AddControllers();
            builder.Services.AddSignalR();

            var connectionString = builder.Configuration.GetConnectionString("TrainingAssessmentPortal")
                ?? "Server=(localdb)\\MSSQLLocalDB;Database=TrainingAssessmentPortalDB;Trusted_Connection=True;TrustServerCertificate=True;";

            builder.Services.AddDbContext<TrainingAssessmentDbContext>(options =>
                options.UseSqlServer(connectionString));

            builder.Services.AddScoped<PortalRepository>();
            builder.Services.AddScoped<ProgramExecutionService>();
            builder.Services.AddSingleton<InteractiveProgramService>();
            builder.Services.AddHttpClient<OllamaAnalysisService>(client => client.Timeout = TimeSpan.FromSeconds(90));
            builder.Services.AddSingleton<WordReportService>();
            builder.Services.AddScoped<DatabaseBackupService>();
            builder.Services.AddSingleton<LiveQuizCoordinator>();
            builder.Services.AddCors(options =>
            {
                options.AddPolicy("TrainingAssessmentPortal", policy =>
                    policy.SetIsOriginAllowed(origin =>
                    {
                        if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri)) return false;
                        var localDevelopment = uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
                            || uri.Host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase);
                        return localDevelopment
                            || origin.Equals("https://atme-training-and-assessment.web.app", StringComparison.OrdinalIgnoreCase)
                            || origin.Equals("https://tap.cseatmeapps.in", StringComparison.OrdinalIgnoreCase)
                            || origin.EndsWith(".cseatmeapps.in", StringComparison.OrdinalIgnoreCase);
                    })
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowCredentials());
            });

            builder.Services.AddOpenApi();
            builder.Services.AddSwaggerGen();

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.MapOpenApi();
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            // Angular development runs on HTTP (localhost:4200) and calls the HTTP API.
            // Redirecting an OPTIONS preflight to HTTPS causes browsers to reject PUT/POST requests.
            if (!app.Environment.IsDevelopment())
            {
                app.UseHttpsRedirection();
            }
            app.UseCors("TrainingAssessmentPortal");
            app.UseAuthorization();
            app.MapControllers();
            app.MapHub<LiveQuizHub>("/hubs/live-quiz");

            app.Run();
        }
    }
}

