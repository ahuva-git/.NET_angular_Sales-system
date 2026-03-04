using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Text;

namespace WebApiProject.MiddleWare
{
    /// <summary>
    /// Middleware לאימות JWT (JSON Web Token)
    /// מגדיר את תצורת האימות ואת כללי הבדיקה של טוקנים
    /// </summary>
    public static class JwtMiddleware
    {
        /// <summary>
        /// הוספת שירותי אימות JWT למערכת:
        /// 1. בודק את תקינות ה-Issuer (מנפיק הטוקן)
        /// 2. בודק את תקינות ה-Audience (קהל היעד)
        /// 3. בודק את תוקף הטוקן (Lifetime)
        /// 4. בודק את חתימת הטוקן (IssuerSigningKey)
        /// 5. מגדיר טיפול מותאם אישית ב-401 Unauthorized ו-403 Forbidden
        /// 
        /// הגדרות נלקחות מ-appsettings.json:
        /// - Jwt:Issuer - מנפיק הטוקן
        /// - Jwt:Audience - קהל היעד
        /// - Jwt:Key - מפתח סודי להצפנה/פענוח
        /// </summary>
        /// <param name="services">אוסף השירותים של האפליקציה</param>
        /// <param name="configuration">אובייקט הגדרות (appsettings.json)</param>
        public static void AddJwtAuthentication(this IServiceCollection services, IConfiguration configuration)
        {
            // הוספת אימות JWT כשירות ברירת המחדל
            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    // הגדרת פרמטרים לאימות טוקן JWT
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,              // ✅ בדיקת מנפיק הטוקן
                        ValidateAudience = true,            // ✅ בדיקת קהל היעד
                        ValidateLifetime = true,            // ✅ בדיקת תוקף הטוקן (לא פג תוקף)
                        ValidateIssuerSigningKey = true,    // ✅ בדיקת החתימה הדיגיטלית

                        ValidIssuer = configuration["Jwt:Issuer"],       // מנפיק מאושר מ-appsettings.json
                        ValidAudience = configuration["Jwt:Audience"],   // קהל יעד מאושר
                        IssuerSigningKey = new SymmetricSecurityKey(     // מפתח סודי לאימות החתימה
                            Encoding.UTF8.GetBytes(configuration["Jwt:Key"])
                        )
                    };


                    // הגדרת טיפול מותאם אישית באירועי אימות
                    options.Events = new JwtBearerEvents
                    {
                        // 🔴 OnChallenge - מופעל כשהמשתמש לא מחובר (401 Unauthorized)
                        OnChallenge = async context =>
                        {
                            context.HandleResponse(); // מונע תגובת ברירת מחדל
                            context.Response.StatusCode = 401;
                            context.Response.ContentType = "application/json";

                            await context.Response.WriteAsJsonAsync(new
                            {
                                Message = "You must be logged in to access this endpoint."
                            });
                        },

                        // 🔴 OnForbidden - מופעל כשהמשתמש לא מורשה (403 Forbidden)
                        // לדוגמה: משתמש מחובר אבל אין לו תפקיד Manager
                        OnForbidden = async context =>
                        {
                            context.Response.StatusCode = 403;
                            context.Response.ContentType = "application/json";

                            await context.Response.WriteAsJsonAsync(new
                            {
                                Message = "You do not have permission to perform this action."
                            });
                        }
                    };
                });
        }
    }
}
