using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using WebApiProject.BLL.Interfaces;
using WebApiProject.Models;
using WebApiProject.Models.DTO;

/// <summary>
/// קונטרולר אימות וכניסה למערכת
/// מטפל בהתחברות משתמשים והפקת טוקני JWT
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IUserBLLService _userService;
    private readonly IConfiguration _config;

    public AuthController(IUserBLLService userService, IConfiguration config)
    {
        _userService = userService;
        _config = config;
    }

    /// <summary>
    /// POST: api/auth/login
    /// מבצע התחברות למערכת:
    /// 1. בודק את נכונות האימייל והסיסמה
    /// 2. מפיק טוקן JWT תקף ל-2 שעות
    /// 3. מחזיר את הטוקן ללקוח
    /// הטוקן כולל: מזהה משתמש, שם משתמש, תפקיד (Manager/User)
    /// </summary>
    /// <param name="request">נתוני התחברות (אימייל + סיסמה)</param>
    /// <returns>טוקן JWT או הודעת שגיאה</returns>
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequestDTO request)
    {
        try
        {
            // בדיקת נכונות האימייל והסיסמה מול בסיס הנתונים
            var user = await _userService.ValidateUser(request.Email, request.Password);

            if (user == null)
                return Unauthorized("Email or password incorrect");

            // יצירת טוקן JWT עבור המשתמש המאומת
            var token = await GenerateJwtToken(user);

            // החזרת הטוקן ללקוח
            return Ok(new { token });
        }
        catch (Exception ex)
        {
            // כל שגיאה בלתי צפויה מוחזרת למשתמש בצורה ברורה
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// יוצר טוקן JWT עבור משתמש מאומת:
    /// 1. בונה Claims (תביעות) עם פרטי המשתמש
    /// 2. חותם את הטוקן במפתח סודי (HmacSha256)
    /// 3. קובע תוקף של 2 שעות
    /// הטוקן מכיל: NameIdentifier, Name, Role
    /// </summary>
    /// <param name="user">אובייקט משתמש מאומת</param>
    /// <returns>מחרוזת טוקן JWT</returns>
    private async Task<string> GenerateJwtToken(User user)
    {
        try
        {
            // יצירת Claims (תביעות) - מידע שנשמר בתוך הטוקן
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),  // מזהה המשתמש
                new Claim(ClaimTypes.Name, user.UserName),                  // שם המשתמש
                new Claim(ClaimTypes.Role, user.Role.ToString())            // תפקיד: Manager או User
            };

            // יצירת מפתח סודי לחתימה על הטוקן (מ-appsettings.json)
            var key = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(_config["Jwt:Key"])
            );

            // הגדרת אלגוריתם החתימה (HMAC SHA256)
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            // בניית הטוקן עם כל הפרמטרים
            var token = new JwtSecurityToken(
                issuer: _config["Jwt:Issuer"],          // מנפיק הטוקן
                audience: _config["Jwt:Audience"],      // קהל היעד
                claims: claims,                         // התביעות
                expires: DateTime.Now.AddHours(2),      // תוקף: 2 שעות מעכשיו
                signingCredentials: creds               // החתימה הדיגיטלית
            );

            // המרת הטוקן למחרוזת והחזרתו
            return new JwtSecurityTokenHandler().WriteToken(token);
        }
        catch (Exception ex)
        {
            // אם יש בעיה בהפקת הטוקן
            throw new Exception(ex.Message);
        }
    }

   
}
