using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;
using WebApiProject.BLL.Interfaces;

namespace WebApiProject.BLL
{
    /// <summary>
    /// שירות שליחת מיילים באמצעות SMTP
    /// משמש לשליחת הודעות לזוכים בהגרלה
    /// הגדרות SMTP נלקחות מ-appsettings.json
    /// </summary>
    public class EmailBLLService : IEmailBLLService
    {
        private readonly IConfiguration _config;

        public EmailBLLService(IConfiguration config)
        {
            _config = config;
        }

        /// <summary>
        /// שולח מייל לזוכה בהגרלה:
        /// 1. מתחבר לשרת SMTP עם אימות
        /// 2. בונה הודעת מזל טוב עם שם המתנה
        /// 3. שולח את המייל לכתובת הזוכה
        /// דורש הגדרות ב-appsettings.json: Email:Smtp, Email:Port, Email:Username, Email:Password
        /// </summary>
        /// <param name="toEmail">כתובת המייל של הזוכה</param>
        /// <param name="giftName">שם המתנה שזכה</param>
        public async Task SendWinnerEmail(string toEmail, string giftName)
        {
            // הגדרת שרת SMTP עם הגדרות מ-appsettings.json
            var smtp = new SmtpClient
            {
                Host = _config["Email:Smtp"],               // כתובת שרת SMTP (למשל: smtp.gmail.com)
                Port = int.Parse(_config["Email:Port"]),    // פורט (בדרך כלל 587 או 465)
                EnableSsl = true,                           // הצפנת SSL/TLS
                Credentials = new NetworkCredential(        // פרטי התחברות
                    _config["Email:Username"],
                    _config["Email:Password"]
                )
            };

            // בניית הודעת המייל
            var mail = new MailMessage
            {
                From = new MailAddress(_config["Email:From"]),  // כתובת השולח
                Subject = "🎉 זכית בהגרלה!",                    // נושא המייל
                Body = $@"
                שלום,

                מזל טוב! 🎊  
                זכית בפרס: {giftName}

                ניצור איתך קשר בהקדם 😊
                ",
                IsBodyHtml = false                               // מייל בטקסט רגיל (לא HTML)
            };

            mail.To.Add(toEmail);  // הוספת כתובת הנמען

            // שליחת המייל באופן אסינכרוני
            await smtp.SendMailAsync(mail);
        }
    }
}
