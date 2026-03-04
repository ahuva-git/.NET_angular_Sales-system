using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Infrastructure;
using Serilog;
using System.Text.Json.Serialization;
using WebApiProject.BLL;
using WebApiProject.BLL.Interfaces;
using WebApiProject.DAL;
using WebApiProject.DAL.Interfaces;
using WebApiProject.Data;
using WebApiProject.MiddleWare;

// ============================================================
// Program.cs - נקודת הכניסה הראשית של האפליקציה
// קובץ זה מגדיר ומתאר את כל תצורת האפליקציה:
// - שירותים (Services)
// - Middleware
// - Database
// - אימות (Authentication)
// - Logging
// ============================================================

var builder = WebApplication.CreateBuilder(args);

// =======================
// Logging - מערכת רישום אירועים
// =======================
// שימוש ב-Serilog למעקב אחר פעילות המערכת
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()                           // רמת לוג מינימלית: Information
    .WriteTo.Console()                                    // כתיבה לקונסול
    .WriteTo.File("Logs/log-.txt",                       // כתיבה לקובץ
        rollingInterval: RollingInterval.Day)             // קובץ חדש כל יום
    .CreateLogger();
builder.Host.UseSerilog();

// הגדרת Logging נוסף של ASP.NET Core
builder.Logging.ClearProviders();                         // מנקה ספקי לוג קיימים
builder.Logging.AddConsole();                             // מוסיף לוג לקונסול
builder.Logging.SetMinimumLevel(LogLevel.Information);   // רמת לוג מינימלית

// =======================
// Dependency Injection - רישום שירותים
// =======================
// DAL (Data Access Layer) - גישה לבסיס נתונים
builder.Services.AddScoped<IDonorDAL, DonorDAL>();
builder.Services.AddScoped<IGiftDAL, GiftDAL>();
builder.Services.AddScoped<IShoppingDAL, ShoppingDAL>();
builder.Services.AddScoped<IUserDAL, UserDAL>();
builder.Services.AddScoped<IRaffleWinnerDAL, RaffleWinnerDAL>();

// BLL (Business Logic Layer) - לוגיקה עסקית
builder.Services.AddScoped<IDonorBLLService, DonorBLLService>();
builder.Services.AddScoped<IGiftBLLService, GiftBLLService>();
builder.Services.AddScoped<IShoppingBLLService, ShoppingBLLService>();
builder.Services.AddScoped<IUserBLLService, UserBLLService>();
builder.Services.AddScoped<IEmailBLLService, EmailBLLService>();

// שירותים נוספים
builder.Services.AddSingleton<RaffleStorageService>();    // Singleton - מופע אחד לכל האפליקציה
builder.Services.AddScoped<RafflePdfBLLService>();
builder.Services.AddScoped<GiftBLLService>();             // רישום כפול של GiftBLLService

// קביעת רישיון QuestPDF (ספריית יצירת PDF)
QuestPDF.Settings.License = LicenseType.Community;

// רישום AutoMapper - מערכת המרת אובייקטים
builder.Services.AddAutoMapper(AppDomain.CurrentDomain.GetAssemblies());


// =======================
// Controllers + Validation - קונטרולרים ואימות נתונים
// =======================
builder.Services.AddControllers();

// הגדרת טיפול מותאם אישית בשגיאות Validation
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        // איסוף כל שגיאות הולידציה
        var errors = context.ModelState
            .Where(e => e.Value.Errors.Count > 0)
            .Select(e => new
            {
                Field = e.Key,                          // שם השדה עם שגיאה
                Message = e.Value.Errors.First().ErrorMessage  // הודעת שגיאה
            });

        // החזרת תגובה מעוצבת 400 Bad Request
        return new BadRequestObjectResult(new
        {
            StatusCode = 400,
            Errors = errors
        });
    };
});


// =======================
// Swagger - תיעוד API אינטראקטיבי
// =======================
builder.Services.AddEndpointsApiExplorer();  // הפעלת תמיכה ב-Swagger

// הגדרת Swagger עם תמיכה ב-JWT
builder.Services.AddSwaggerGen(options =>
{
    // הגדרת סכמת אימות JWT
    options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "Authorization",                        // שם הכותרת
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "Bearer",                             // סוג האימות
        BearerFormat = "JWT",                          // פורמט הטוקן
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "Enter JWT token like: Bearer {token}"  // הנחייה למשתמש
    });

    // דרישה להוסיף טוקן לכל בקשה
    options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()  // לא נדרש scope מיוחד
        }
    });
});

// הגדרת Serialization של Enums כטקסט (לא מספרים)
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(
            new JsonStringEnumConverter());  // ממיר Enums ל-String ב-JSON
    });

// =======================
// CORS - Cross-Origin Resource Sharing
// =======================
// מאפשר לאפליקציית Angular לגשת ל-API
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngular",
        policy =>
            policy.WithOrigins(                          // כתובות מורשות
                "http://localhost:4200",                 // Angular Dev Server
                "https://localhost:4200",
                "http://localhost:61558",                // IIS Express
                "http://localhost:60588")
                  .AllowAnyHeader()                      // מאפשר כל כותרת
                  .AllowAnyMethod());                    // מאפשר כל פעולה (GET, POST, וכו')
});

// =======================
// Database - חיבור לבסיס נתונים
// =======================
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(
    // Connection Strings (רק אחד פעיל):
    //"Data Source=SRV2\\PUPILS;DataBase=project_db;Integrated Security=True;Encrypt=True;Trust Server Certificate=True"
    //"Data Source=SRV2\\PUPILS;DataBase=project_db2;Integrated Security=True;Encrypt=True;Trust Server Certificate=True"
    //"Data Source=DESKTOP-1VUANBN;Initial Catalog=WebApiDB;Integrated Security=True;Encrypt=True;Trust Server Certificate=True"
    //"Data Source=שני;DataBase=project_db;Integrated Security=True;Encrypt=True;Trust Server Certificate=True"
    "Data Source=DESKTOP-EODP1E7;Initial Catalog=WebApiDB;Integrated Security=True;Encrypt=True;Trust Server Certificate=True"
    ));

// =======================
// JWT Authentication - אימות באמצעות טוקנים
// =======================
builder.Services.AddJwtAuthentication(builder.Configuration);  // Extension Method מ-JwtMiddleware

// הגדרת Authorization (אימות הרשאות/תפקידים)
builder.Services.AddAuthorization();

// =======================
// Build App - בניית האפליקציה
// =======================
var app = builder.Build();

// הפעלת CORS (חובה לפני Middleware אחר!)
app.UseCors("AllowAngular");

// =======================
// Middleware Pipeline - שרשרת עיבוד בקשות
// =======================
// סדר ה-Middleware חשוב! כל בקשה עוברת דרך הסדר הזה:

if (app.Environment.IsDevelopment())
{
    // בסביבת פיתוח - הפעלת Swagger
    app.UseSwagger();        // API Documentation
    app.UseSwaggerUI();      // Swagger UI Interface
}

// =======================
// Global Exception Handler - טיפול בשגיאות
// =======================
// 1. טיפול גלובלי בשגיאות (חייב להיות ראשון!)
app.ConfigureErrorHandling();

// 2. אימות - בדיקת טוקן JWT
app.UseAuthentication();

// 3. הרשאות - בדיקת תפקידים (Manager/User)
app.UseAuthorization();

// 4. הפניה ל-HTTPS
app.UseHttpsRedirection();

// 5. ניתוב לקונטרולרים
app.MapControllers();

// =======================
// הרצת האפליקציה
// =======================
app.Run();  // מתחיל להקשיב לבקשות HTTP