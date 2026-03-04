using AutoMapper;
using Microsoft.EntityFrameworkCore;
using WebApiProject.BLL.Interfaces;
using WebApiProject.DAL;
using WebApiProject.DAL.Interfaces;
using WebApiProject.Models;
using WebApiProject.Models.DTO;

namespace WebApiProject.BLL
{
    public class GiftBLLService : IGiftBLLService
    {
        private readonly IGiftDAL giftDal;
        private readonly IShoppingDAL shoppingDal;
        private readonly IMapper mapper;
        private readonly ILogger<GiftBLLService> logger;
        private readonly IEmailBLLService emailService;
        private readonly RaffleStorageService raffleStorageService; // 🔹 מוסיפים שירות זוכים
        private readonly IRaffleWinnerDAL raffleWinnerDal;


        public GiftBLLService(IGiftDAL giftDal, IShoppingDAL shoppingDal, IMapper mapper, ILogger<GiftBLLService> logger, IEmailBLLService emailService, RaffleStorageService raffleStorageService, IRaffleWinnerDAL raffleWinnerDal)
        {
            this.giftDal = giftDal;
            this.mapper = mapper;
            this.logger = logger;
            this.shoppingDal = shoppingDal;
            this.emailService = emailService;
            this.raffleStorageService = raffleStorageService;
            this.raffleWinnerDal = raffleWinnerDal;
        }

        /// <summary>
        /// שולף את כל המתנות מבסיס הנתונים כולל ניווט לתורם ורשימת רכישות
        /// ממיר את התוצאות ל-GiftGetDTO באמצעות AutoMapper
        /// </summary>
        /// <returns>רשימה של כל המתנות</returns>
        public async Task<List<GiftGetDTO>> Get()
        {
            try
            {
                logger.LogInformation("Fetching all gifts");

                var gifts = await giftDal.Get();
                return mapper.Map<List<GiftGetDTO>>(gifts);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error while fetching gifts");
                throw;
            }
        }
        /// <summary>
        /// שולף מתנות מסוננות לפי קריטריונים:
        /// - שם מתנה (חיפוש חלקי עם Contains)
        /// - קטגוריה (התאמה מדויקת)
        /// - שם תורם (חיפוש חלקי)
        /// - מיון לפי מחיר או כמות רכישות (עולה/יורד)
        /// </summary>
        /// <param name="filter">אובייקט סינון עם פרמטרים אופציונליים</param>
        /// <returns>רשימת מתנות מסוננות וממוינות</returns>
        public async Task<List<GiftGetDTO>> GetFiltered(GiftFilterDTO filter)
        {
            try
            {
                // לוג בתחילת הפעולה עם הפרמטרים שנשלחו
                logger.LogInformation("Fetching filtered gifts with criteria: {@Filter}", filter);

                var query = giftDal.Query();

                // סינון לפי שם המתנה
                if (!string.IsNullOrEmpty(filter.GiftName))
                {
                    query = query.Where(g => g.Name.Contains(filter.GiftName));
                }

                // סינון לפי קטגוריה
                if (!string.IsNullOrEmpty(filter.Category))
                {
                    query = query.Where(g => g.Category == filter.Category);
                }

                // סינון לפי שם התורם
                if (!string.IsNullOrEmpty(filter.DonorName))
                {
                    query = query.Where(g => g.Donor.Name.Contains(filter.DonorName));
                }

                // מיון
                if (filter.SortBy.HasValue)
                {
                    query = filter.SortBy switch
                    {
                        GiftSortBy.Price =>
                            filter.Desc ? query.OrderByDescending(g => g.CardPrice)
                                        : query.OrderBy(g => g.CardPrice),

                        // מיון לפי כמות רכישות (ספירת רשימת ה-Shoppings)
                        GiftSortBy.PurchasesCount =>
                            filter.Desc ? query.OrderByDescending(g => g.Shoppings.Count)
                                        : query.OrderBy(g => g.Shoppings.Count),

                        _ => query
                    };
                }

                // הרצת השאילתה מול בסיס הנתונים
                var list = await query.ToListAsync();

                logger.LogInformation("Successfully fetched {Count} gifts", list.Count);

                // מיפוי ל-DTO
                return mapper.Map<List<GiftGetDTO>>(list);
            }
            catch (Exception ex)
            {
                // רישום שגיאה במידה ומשהו נכשל (למשל בעיית תקשורת עם ה-DB)
                logger.LogError(ex, "An error occurred while fetching filtered gifts with filter: {@Filter}", filter);

                // זריקת השגיאה הלאה כדי שה-Controller יוכל להחזיר תגובה מתאימה (כמו 500)
                throw;
            }
        }

        /// <summary>
        /// שולף מתנה אחת לפי מזהה כולל ניווט לתורם ורכישות
        /// ממיר ל-GiftGetDTO באמצעות AutoMapper
        /// </summary>
        /// <param name="id">מזהה המתנה</param>
        /// <returns>פרטי המתנה או null אם לא נמצאה</returns>
        public async Task<GiftGetDTO?> GetById(int id)
        {
            try
            {
                logger.LogInformation("Fetching gift {Id}", id);

                var gift = await giftDal.GetById(id);
                return mapper.Map<GiftGetDTO?>(gift);
            }
            //אם רוצים שלכל מתנה יהיה את רשימת הקניות שלה עם פרטי המשתמשים
            //try
            //{
            //    logger.LogInformation("Fetching gift {Id}", id);

            //    // שולף את המתנה כולל רכישות
            //    var gift = await giftDal.GetById(id);
            //    if (gift == null)
            //        return null;

            //    // ממפה ל-DTO
            //    var giftDto = mapper.Map<GiftDTO>(gift);

            //    // 🔹 ממפה את רשימת הרכישות עם פרטי המשתמש בצורה שטוחה
            //    giftDto.Shoppings = gift.Shoppings
            //        .Select(s => mapper.Map<ShoppingDTO>(s))
            //        .ToList();

            //    return giftDto;
            //}

            catch (Exception ex)
            {
                logger.LogError(ex, "Error while fetching gift {Id}", id);
                throw;
            }
        }

        /// <summary>
        /// מוסיף מתנה חדשה לבסיס הנתונים
        /// ממיר מ-GiftDTO לאובייקט Gift Entity באמצעות AutoMapper
        /// </summary>
        /// <param name="giftDTO">נתוני המתנה להוספה</param>
        public async Task Add(GiftDTO giftDTO)
        {
            try
            {
                logger.LogInformation("Adding new gift");

                var gift = mapper.Map<Gift>(giftDTO);
                await giftDal.Add(gift);

                logger.LogInformation("Gift added successfully");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error while adding gift");
                throw;
            }
        }

        /// <summary>
        /// מעדכן מתנה קיימת בבסיס הנתונים
        /// ממיר מ-GiftDTO לאובייקט קיים באמצעות AutoMapper
        /// </summary>
        /// <param name="id">מזהה המתנה לעדכון</param>
        /// <param name="giftDTO">נתונים מעודכנים</param>
        /// <returns>true אם עודכן בהצלחה, false אם המתנה לא נמצאה</returns>
        public async Task<bool> Put(int id, GiftDTO giftDTO)
        {
            try
            {
                logger.LogInformation("Updating gift {Id}", id);

                var gift = await giftDal.GetById(id);
                if (gift == null)
                {
                    logger.LogWarning("Gift {Id} not found", id);
                    return false;
                }

                mapper.Map(giftDTO, gift);
                await giftDal.Put(gift);

                logger.LogInformation("Gift {Id} updated successfully", id);
                return true;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error while updating gift {Id}", id);
                throw;
            }
        }

        /// <summary>
        /// מוחק מתנה מבסיס הנתונים
        /// המחיקה תצליח רק אם אין רכישות מאושרות למתנה (טיוטות מותר למחוק)
        /// </summary>
        /// <param name="id">מזהה המתנה למחיקה</param>
        /// <returns>true אם נמחקה בהצלחה, false אם נכשלה</returns>
        public async Task<bool> Delete(int id)
        {
            try
            {
                logger.LogInformation("Deleting gift {Id}", id);

                var result = await giftDal.Delete(id);

                if (!result)
                {
                    logger.LogWarning("Delete failed for gift {Id} (not found or has shoppings)", id);
                }

                return result;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error while deleting gift {Id}", id);
                throw;
            }
        }


        /// <summary>
        /// מבצע הגרלה למתנה ספציפית:
        /// 1. בודק שהמתנה קיימת ולא הוגרלה קודם
        /// 2. מסנן רק רכישות מאושרות (IsDraft = false)
        /// 3. בוחר זוכה אקראי מהרשימה
        /// 4. שומר את הזוכה בטבלת RaffleWinners
        /// 5. מעדכן IsRaffled = true
        /// 6. שולח מייל לזוכה
        /// </summary>
        /// <param name="giftId">מזהה המתנה להגרלה</param>
        /// <returns>פרטי הזוכה או null אם ההגרלה נכשלה</returns>
        public async Task<RaffleResultDTO?> RaffleGift(int giftId)
        {
            try
            {
                var gift = await giftDal.Query()
                    .Include(g => g.Shoppings)
                        .ThenInclude(s => s.User)
                    .FirstOrDefaultAsync(g => g.Id == giftId);

                if (gift == null)
                {
                    logger.LogWarning("Gift {GiftId} not found", giftId);
                    return null;
                }

                if (gift.IsRaffled)
                {
                    logger.LogWarning("Gift {GiftId} has already been raffled", giftId);
                    return null;
                }

                var confirmShoppings = gift.Shoppings.Where(s => !s.IsDraft).ToList();
                if (!confirmShoppings.Any())
                {
                    logger.LogWarning("No confirmed shoppings for gift {GiftId}", giftId);
                    return null;
                }

                var random = new Random();
                var winnerShopping = confirmShoppings[random.Next(confirmShoppings.Count)];

                var result = new RaffleResultDTO
                {
                    GiftId = gift.Id,
                    GiftName = gift.Name,
                    WinnerUserId = winnerShopping.UserId,
                    WinnerUserName = winnerShopping.User.UserName,
                    WinnerEmail = winnerShopping.User.Email
                };

                // 🔹 Save winner to database
                var raffleWinner = new RaffleWinner
                {
                    GiftId = gift.Id,
                    UserId = winnerShopping.UserId,
                    RaffleDate = DateTime.Now
                };
                await raffleWinnerDal.AddAsync(raffleWinner);

                gift.IsRaffled = true;
                await giftDal.Put(gift);

                try
                {
                    await emailService.SendWinnerEmail(result.WinnerEmail, result.GiftName);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to send email to winner {Email}", result.WinnerEmail);
                }

                return result;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error while raffling gift {GiftId}", giftId);
                throw;
            }
        }
        /// <summary>
        /// מבצע הגרלה לכל המתנות שטרם הוגרלו:
        /// 1. שולף כל מתנה ובודק אם IsRaffled = false
        /// 2. מדלג על מתנות ללא רכישות מאושרות
        /// 3. בוחר זוכה אקראי לכל מתנה
        /// 4. מעדכן IsRaffled = true לכל מתנה שהוגרלה
        /// 5. שולח מיילים לכל הזוכים
        /// </summary>
        /// <returns>רשימת כל הזוכים</returns>
        public async Task<List<RaffleResultDTO>> RaffleAll()
        {
            try
            {
                var gifts = await giftDal.Get();
                var results = new List<RaffleResultDTO>();

                foreach (var gift in gifts)
                {
                    if (gift.IsRaffled)
                        continue; // כבר הוגרל

                    var shoppings = gift.Shoppings.Where(s => !s.IsDraft).ToList();
                    if (!shoppings.Any())
                        continue; // אין רכישות מאושרות

                    var random = new Random();
                    var winnerShopping = shoppings[random.Next(shoppings.Count)];

                    var result = new RaffleResultDTO
                    {
                        GiftId = gift.Id,
                        GiftName = gift.Name,
                        WinnerUserId = winnerShopping.UserId,
                        WinnerUserName = winnerShopping.User.UserName,
                        WinnerEmail = winnerShopping.User.Email
                    };

                    results.Add(result);

                    // עדכון המתנה כבוצעה הגרלה
                    gift.IsRaffled = true;
                    await giftDal.Put(gift);

                    // שליחת מייל לניצח
                    try
                    {
                        await emailService.SendWinnerEmail(result.WinnerEmail, result.GiftName);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Failed to send email to winner {Email} for gift {GiftId}", result.WinnerEmail, gift.Id);
                    }
                }

                return results;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error while raffling all gifts");
                throw;
            }
        }
    
        /// <summary>
        /// מחשב את סך ההכנסות מכל הרכישות המאושרות (לא טיוטות)
        /// נוסחה: Σ(Quantity × CardPrice) לכל רכישה מאושרת
        /// שולף מתנות עם רכישות מאושרות בלבד
        /// </summary>
        /// <returns>סכום כולל של הכנסות במטבע מקומי</returns>
        public async Task<int> GetTotalIncome()
        {
            try
            {
                logger.LogInformation("Calculating total income from all confirmed shoppings");

                var gifts = await giftDal.Query()
                    .Include(g => g.Shoppings.Where(s => !s.IsDraft))
                    .ToListAsync();

                int totalIncome = 0;

                foreach (var gift in gifts)
                {
                    var confirmShoppings = gift.Shoppings.Where(s => !s.IsDraft).ToList();

                    // חישוב: סכום של (כמות × מחיר כרטיס) לכל רכישה מאושרת
                    totalIncome += confirmShoppings.Sum(s => s.Quantity * gift.CardPrice);
                }

                logger.LogInformation("Total income calculated: {TotalIncome} from {GiftsCount} gifts",
                    totalIncome, gifts.Count);

                return totalIncome;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error while calculating total income");
                throw;
            }
        }
    }
}




       