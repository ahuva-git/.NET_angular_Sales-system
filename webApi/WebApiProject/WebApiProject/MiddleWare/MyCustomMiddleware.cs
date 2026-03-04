namespace WebApiProject.MiddleWare
{
using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

/// <summary>
/// Middleware הדגמה מותאם אישית ללוגינג ומעקב אחרי בקשות HTTP
/// רושם את נתיב הבקשה לפני ואחרי הטיפול בה
/// שימושי למעקב, ביקורת, או פעולות נוספות לפני/אחרי הבקשה
/// </summary>
public class MyCustomMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<MyCustomMiddleware> _logger;

        /// <summary>
        /// בנאי ה-Middleware
        /// </summary>
        /// <param name="next">ה-Middleware הבא בשרשרת (Pipeline)</param>
        /// <param name="logger">שירות לוגינג</param>
        public MyCustomMiddleware(RequestDelegate next, ILogger<MyCustomMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        /// <summary>
        /// מתודה ראשית שמטפלת בכל בקשת HTTP:
        /// 1. רושמת את נתיב הבקשה (Request.Path) לפני הטיפול
        /// 2. מעבירה את הבקשה ל-Middleware הבא בשרשרת (_next)
        /// 3. רושמת הודעה לאחר שהתגובה נשלחה
        /// </summary>
        /// <param name="context">הקונטקסט של הבקשה הנוכחית</param>
        public async Task InvokeAsync(HttpContext context)
        {
            // פעולה לפני העברת הבקשה הלאה
            _logger.LogInformation($"Request path: {context.Request.Path}");

            // העברת הבקשה למידלוור הבא ב-pipeline
            await _next(context);

            // פעולה אחרי
            _logger.LogInformation("Response sent");
        }
    }

}

