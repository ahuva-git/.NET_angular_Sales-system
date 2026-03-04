using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

namespace WebApiProject.MiddleWare
{
    public static class ErrorHandlingMiddleware
    {
    /// <summary>
    /// Middleware גלובלי לטיפול בשגיאות במרוכז:
    /// - InvalidOperationException → 400 Bad Request
    /// - KeyNotFoundException → 404 Not Found
    /// - שגיאות אחרות → 500 Internal Server Error
    /// רושם את השגיאה ללוג ומחזיר תגובת JSON מעוצבת ללקוח
    /// </summary>
    /// <param name="app">אובייקט ApplicationBuilder</param>
    public static void ConfigureErrorHandling(this IApplicationBuilder app)
        {
            app.UseExceptionHandler(appError =>
            {
                appError.Run(async context =>
                {
                    // שליפת השגיאה שנזרקה מהמערכת
                    var exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;
                    
                    // שליפת שירות הלוגינג מה-DI Container
                    var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();

                    // ברירת מחדל: 500 Internal Server Error
                    int statusCode = 500;
                    string message = "An unexpected error occurred.";

                    // בדיקת סוג השגיאה והחזרת קוד סטטוס מתאים
                    switch (exception)
                    {
                        case InvalidOperationException ioe:
                            // פעולה לא חוקית - בעיה בלוגיקה עסקית
                            statusCode = 400;
                            message = ioe.Message;
                            break;
                        case KeyNotFoundException knf:
                            // פריט לא נמצא - משאב לא קיים
                            statusCode = 404;
                            message = knf.Message;
                            break;
                    }

                    // רישום השגיאה בלוג לצורכי מעקב
                    logger.LogError(exception, "Error occurred");

                    // החזרת תגובת JSON עם הודעת שגיאה ללקוח
                    context.Response.StatusCode = statusCode;
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsJsonAsync(new
                    {
                        Message = message
                    });
                });
            });
        }
    }
}
