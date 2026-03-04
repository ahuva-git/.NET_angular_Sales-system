using AutoMapper;
using Microsoft.EntityFrameworkCore;
using WebApiProject.BLL.Interfaces;
using WebApiProject.DAL.Interfaces;
using WebApiProject.Models;
using WebApiProject.Models.DTO;

namespace WebApiProject.BLL
{
    public class ShoppingBLLService : IShoppingBLLService
    {
        private readonly IShoppingDAL shoppingDal;
        private readonly IMapper mapper;
        private readonly IGiftBLLService giftBLL;
        private readonly ILogger<ShoppingBLLService> logger;

        public ShoppingBLLService(IShoppingDAL shoppingDal, IMapper mapper, IGiftBLLService giftBLL, ILogger<ShoppingBLLService> logger)
        {
            this.shoppingDal = shoppingDal;
            this.mapper = mapper;
            this.logger = logger;
            this.giftBLL = giftBLL;
        }

       

        /// <summary>
        /// שולף את כל הרכישות (טיוטות + מאושרות) מבסיס הנתונים
        /// לא מסנן לפי IsDraft - מחזיר הכל
        /// שימושי למשתמשים רגילים שרוצים לראות את כל הרכישות שלהם
        /// </summary>
        /// <returns>רשימת כל הרכישות</returns>
        public async Task<List<ShoppingDTO>> GetAll()
        {
            try
            {
                logger.LogInformation("Fetching all shoppings (drafts and confirmed)");

                var shoppings = await shoppingDal.Get();
                return mapper.Map<List<ShoppingDTO>>(shoppings);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error while fetching all shoppings");
                throw;
            }
        }
        /// <summary>
        /// שולף רק רכישות מאושרות (IsDraft = false)
        /// מסנן את הטיוטות ומחזיר רק רכישות שאושרו
        /// שימושי למנהלים לראות רק רכישות סופיות
        /// </summary>
        /// <returns>רשימת רכישות מאושרות בלבד</returns>
        public async Task<List<ShoppingDTO>> Get()
        {
            try
            {
                logger.LogInformation("Fetching all confirmed shoppings");

                var shoppings = await shoppingDal.Get();
                var confirmed = shoppings.Where(s => !s.IsDraft).ToList();

                return mapper.Map<List<ShoppingDTO>>(confirmed);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error while fetching shoppings");
                throw;
            }
        }

        /// <summary>
        /// שולף רכישות ממוינות לפי:
        /// - Price: מיון לפי מחיר המתנה (CardPrice)
        /// - Popularity: מיון לפי סך כמות נרכשת לכל מתנה (GroupBy + Sum)
        /// תומך במיון עולה/יורד (Desc)
        /// </summary>
        /// <param name="sort">אובייקט מיון עם פרמטרים</param>
        /// <returns>רשימת רכישות ממוינות</returns>
        public async Task<List<ShoppingDTO>> GetSorted(ShoppingSortDTO sort)
        {
            try
            {
                logger.LogInformation("Fetching shoppings sorted by {@Sort}", sort);

                var query = shoppingDal.Query();

                if (sort.SortBy.HasValue)
                {
                    if (sort.SortBy == ShoppingSortBy.Price)
                    {
                        query = sort.Desc
                            ? query.OrderByDescending(s => s.Gift.CardPrice)
                            : query.OrderBy(s => s.Gift.CardPrice);
                    }
                    else if (sort.SortBy == ShoppingSortBy.Popularity)
                    {
                        // כאן עושים קיבוץ + סכום Quantity, ואז שליפה של כל הרשומות של המתנה לפי סכום Quantity
                        var popularityQuery = await query
                            .GroupBy(s => s.GiftId)
                            .Select(g => new
                            {
                                GiftId = g.Key,
                                TotalQuantity = g.Sum(x => x.Quantity)
                            })
                            .OrderByDescending(g => g.TotalQuantity)
                            .ToListAsync();

                        // מביאים את כל הרשומות לפי סדר הפופולריות
                        var sortedList = new List<ShoppingDTO>();
                        foreach (var item in popularityQuery)
                        {
                            var shoppingsOfGift = await query
                                .Where(s => s.GiftId == item.GiftId)
                                .ToListAsync();

                            sortedList.AddRange(mapper.Map<List<ShoppingDTO>>(shoppingsOfGift));
                        }

                        return sortedList;
                    }
                }

                var shoppings = await query.ToListAsync();
                return mapper.Map<List<ShoppingDTO>>(shoppings);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error while fetching sorted shoppings");
                throw;
            }
        }
        /// <summary>
        /// שולף את כל הרכישות של מתנה ספציפית
        /// מחזיר רשימה של כל מי שקנה את המתנה
        /// </summary>
        /// <param name="id">מזהה המתנה</param>
        /// <returns>רשימת רכישות למתנה זו</returns>
        public async Task<List<ShoppingDTO?>> GetGiftById(int id)
        {
            try
            {
                logger.LogInformation("Fetching shopping {Id}", id);
                var shopping = await shoppingDal.GetGiftById(id);
                return mapper.Map<List<ShoppingDTO?>>(shopping);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error while fetching shopping {Id}", id);
                throw;
            }
        }

        /// <summary>
        /// מוסיף רכישה חדשה למערכת (כטיוטה - IsDraft = true)
        /// בודק שהמתנה קיימת ולא הוגרלה (IsRaffled = false)
        /// זורק שגיאה אם המתנה כבר הוגרלה או לא קיימת
        /// </summary>
        /// <param name="shoppingDTO">נתוני הרכישה החדשה</param>
        public async Task Add(ShoppingDTO shoppingDTO)
        {
            try
            {
                logger.LogInformation("Adding shopping");

                var gift = await giftBLL.GetById(shoppingDTO.GiftId);
                if (gift == null)
                {
                    logger.LogWarning("Gift {GiftId} does not exist", shoppingDTO.GiftId);
                    throw new Exception("Gift does not exist."); // 🔴 שינוי קריטי: בודק קיום מתנה
                }

                if (gift.IsRaffled)
                {
                    logger.LogWarning("Cannot buy a gift {GiftId} that has already been raffled", shoppingDTO.GiftId);
                    throw new Exception("Cannot buy a gift that has already been raffled."); // 🔴 שינוי קריטי: בודק הגרלה
                }

                var shopping = mapper.Map<Shopping>(shoppingDTO);
                shopping.IsDraft = true;
                await shoppingDal.Add(shopping);

                logger.LogInformation("Shopping added successfully");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error while adding shopping");
                throw;
            }
        }


     

        /// <summary>
        /// מעדכן רכישה קיימת (רק אם היא טיוטה)
        /// בודק שהמתנה קיימת ולא הוגרלה
        /// זורק שגיאה אם המתנה הוגרלה או לא קיימת
        /// </summary>
        /// <param name="id">מזהה הרכישה לעדכון</param>
        /// <param name="shoppingDTO">נתונים מעודכנים</param>
        /// <returns>true אם עודכן, false אם לא נמצאה</returns>
        public async Task<bool> Put(int id, ShoppingDTO shoppingDTO)
        {
            try
            {
                logger.LogInformation("Updating shopping {Id}", id);

                var shopping = await shoppingDal.GetById(id);
                if (shopping == null)
                    return false;

                var gift = await giftBLL.GetById(shoppingDTO.GiftId);
                if (gift == null)
                {
                    logger.LogWarning("Gift {GiftId} does not exist", shoppingDTO.GiftId);
                    throw new Exception("Gift does not exist."); // 🔴 שינוי קריטי
                }

                if (gift.IsRaffled)
                {
                    logger.LogWarning("Cannot update a shopping for a gift {GiftId} that has already been raffled", shoppingDTO.GiftId);
                    throw new Exception("Cannot update a shopping for a gift that has already been raffled."); // 🔴 שינוי קריטי
                }

                mapper.Map(shoppingDTO, shopping); // ✅ מיפוי DTO לשמירת שמות שדות
                await shoppingDal.Put(shopping);

                logger.LogInformation("Shopping {Id} updated successfully", id);
                return true;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error while updating shopping {Id}", id);
                throw;
            }
        }

        /// <summary>
        /// שולף רכישה אחת לפי מזהה
        /// כולל ניווט למשתמש ומתנה
        /// </summary>
        /// <param name="id">מזהה הרכישה</param>
        /// <returns>פרטי הרכישה או null</returns>
        public async Task<ShoppingDTO?> GetById(int id)
        {
            try
            {
                logger.LogInformation("Fetching shopping {Id}", id);
                var shopping = await shoppingDal.GetById(id);
                return mapper.Map<ShoppingDTO?>(shopping);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error while fetching shopping {Id}", id);
                throw;
            }
        }

        /// <summary>
        /// בודק אם יש רכישות מאושרות למתנה ספציפית
        /// שימושי לבידוק אם ניתן למחוק מתנה (אסור אם יש רכישות מאושרות)
        /// </summary>
        /// <param name="giftId">מזהה המתנה</param>
        /// <returns>true אם יש רכישות מאושרות, false אחרת</returns>
        public async Task<bool> HasNonDraftShoppingsForGift(int giftId)
        {
            try
            {
                var shoppings = await shoppingDal.Get();
                return shoppings.Any(s => s.GiftId == giftId && !s.IsDraft); // 🔴 שינוי קריטי: בודק Draft
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error while checking shoppings for gift {GiftId}", giftId);
                throw;
            }
        }

        

        /// <summary>
        /// מוחק רכישה מבסיס הנתונים
        /// מותר למחוק רק טיוטות (IsDraft = true)
        /// אסור למחוק רכישה מאושרת
        /// </summary>
        /// <param name="id">מזהה הרכישה למחיקה</param>
        /// <returns>true אם נמחקה, false אם לא נמצאה או מאושרת</returns>
        public async Task<bool> Delete(int id)
        {
            // בדיקה אם הרכישה קיימת
            var shopping = await shoppingDal.GetById(id);
            if (shopping == null)
                return false; // לא קיים רכישה עם מזהה זה

            // בדיקה אם הרכישה אושרה - אם כן, אסור למחוק
            if (!shopping.IsDraft)
                return false; // לא ניתן למחוק רכישה שאושרה

            // מבצעים מחיקה
            return await shoppingDal.Delete(id);
        }


        /// <summary>
        /// מאשר רכישה טיוטה - משנה IsDraft ל-false
        /// לאחר אישור, לא ניתן לעדכן או למחוק את הרכישה
        /// זורק שגיאה אם הרכישה כבר אושרה
        /// שקול לסגירת עגלת קניות/תשלום
        /// </summary>
        /// <param name="id">מזהה הרכישה לאישור</param>
        /// <returns>true אם אושרה בהצלחה</returns>
        public async Task<bool> ConfirmShopping(int id)
        {
            var shopping = await shoppingDal.GetById(id);
            if (shopping == null)
                throw new Exception("Shopping does not exist.");

            if (!shopping.IsDraft)
                throw new Exception("Shopping was already confirmed."); // 🔴 שינוי קריטי

            shopping.IsDraft = false;// אישור רכישה
            await shoppingDal.Put(shopping);

            return true;
        }


    }
}