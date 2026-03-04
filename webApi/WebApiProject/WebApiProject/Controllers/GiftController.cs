using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebApiProject.BLL.Interfaces;
using WebApiProject.Models.DTO;

namespace WebApiProject.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    

    public class GiftController : ControllerBase
    {
        private readonly IGiftBLLService giftBLL;
        private readonly IDonorBLLService donorBLL;
        private readonly IShoppingBLLService shoppingBLL;

        public GiftController(IGiftBLLService giftBLL, IDonorBLLService donorBLL, IShoppingBLLService shoppingBLL)
        {
            this.giftBLL = giftBLL;
            this.donorBLL = donorBLL;
            this.shoppingBLL = shoppingBLL;
        }

        /// <summary>
        /// GET: api/gift
        /// שולף את כל המתנות במערכת ללא סינון
        /// </summary>
        /// <returns>רשימה של כל המתנות כ-GiftGetDTO</returns>
        [HttpGet]
        public async Task<IActionResult> Get()
        {
            try
            {
                return Ok(await giftBLL.Get());
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        /// <summary>
        /// GET: api/gift/filter
        /// שולף מתנות מסוננות לפי קריטריונים: שם מתנה, קטגוריה, שם תורם, מיון
        /// </summary>
        /// <param name="filter">אובייקט סינון עם פרמטרים אופציונליים</param>
        /// <returns>רשימת מתנות מסוננות</returns>
        [HttpGet("filter")]
        public async Task<IActionResult> GetFiltered([FromQuery] GiftFilterDTO filter)
        {
            try
            {
                var result = await giftBLL.GetFiltered(filter);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        /// <summary>
        /// GET: api/gift/5
        /// שולף מתנה אחת לפי מזהה - דורש אימות (משתמש מחובר)
        /// </summary>
        /// <param name="id">מזהה המתנה</param>
        /// <returns>פרטי המתנה או NotFound אם לא נמצאה</returns>
        [HttpGet("{id}")]
        [Authorize]
        public async Task<IActionResult> Get(int id)
        {
            try
            {
                if (id <= 0)
                    return BadRequest("Invalid id");

                var gift = await giftBLL.GetById(id);
                if (gift == null)
                    return NotFound($"Gift with id {id} does not exist.");

                return Ok(gift);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        /// <summary>
        /// POST: api/gift
        /// הוספת מתנה חדשה למערכת - זמין רק למנהלים
        /// מוודא שהתורם קיים ושהשם תואם למזהה התורם
        /// </summary>
        /// <param name="giftDTO">נתוני המתנה החדשה</param>
        /// <returns>הודעת הצלחה או שגיאה</returns>
        [HttpPost]
        [Authorize(Roles = "Manager")]
        public async Task<IActionResult> Post([FromBody] GiftDTO giftDTO)
        {
            try
            {
                DonorDTO? donor = await donorBLL.GetById(giftDTO.DonorId);
                if (donor == null)
                    return NotFound($"Donor with id {giftDTO.DonorId} does not exist.");

                if (donor.Name != giftDTO.DonorName)
                    return NotFound($"Donor with name {giftDTO.DonorName} not match to donorId.");

                await giftBLL.Add(giftDTO);
                return Ok("Gift added successfully.");
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        /// <summary>
        /// PUT: api/gift/5
        /// עדכון מתנה קיימת - זמין רק למנהלים
        /// מוודא שהמתנה והתורם החדש קיימים
        /// </summary>
        /// <param name="id">מזהה המתנה לעדכון</param>
        /// <param name="giftDTO">נתונים מעודכנים</param>
        /// <returns>הודעת הצלחה או שגיאה</returns>
        [HttpPut("{id}")]
        [Authorize(Roles = "Manager")]
        public async Task<IActionResult> Put(int id, [FromBody] GiftDTO giftDTO)
        {
            try
            {
                if (id <= 0)
                    return BadRequest("Invalid id");

                var existing = await giftBLL.GetById(id);
                if (existing == null)
                    return NotFound($"Gift with id {id} does not exist.");

                var donor = await donorBLL.GetById(giftDTO.DonorId);
                if (donor == null)
                    return NotFound($"Donor with id {giftDTO.DonorId} does not exist.");

                if (donor.Name != giftDTO.DonorName)
                    return NotFound($"Donor with name {giftDTO.DonorName} not match to donorId.");

                var updated = await giftBLL.Put(id, giftDTO);
                if (!updated)
                    return BadRequest("Failed to update gift.");

                return Ok("Gift updated successfully.");
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        /// <summary>
        /// DELETE: api/gift/5
        /// מחיקת מתנה מהמערכת - זמין רק למנהלים
        /// אסור למחוק מתנה שיש עליה רכישות מאושרות (לא טיוטות)
        /// </summary>
        /// <param name="id">מזהה המתנה למחיקה</param>
        /// <returns>הודעת הצלחה או שגיאה</returns>
        [HttpDelete("{id}")]
        [Authorize(Roles = "Manager")]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                if (id <= 0)
                    return BadRequest("Invalid id");

                var existing = await giftBLL.GetById(id);
                if (existing == null)
                    return NotFound($"Gift with id {id} does not exist.");

                if (await shoppingBLL.HasNonDraftShoppingsForGift(id))
                    return BadRequest("Cannot delete gift that has already been shopped.");

                var deleted = await giftBLL.Delete(id);
                if (!deleted)
                    return BadRequest("Failed to delete gift.");

                return Ok("Gift deleted successfully.");
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }

        }

        /// <summary>
        /// POST: api/gift/5/raffle
        /// ביצוע הגרלה למתנה ספציפית - זמין רק למנהלים
        /// בוחר זוכה אקראי מבין הרכישות המאושרות, שולח מייל לזוכה ומעדכן IsRaffled=true
        /// </summary>
        /// <param name="id">מזהה המתנה להגרלה</param>
        /// <returns>פרטי הזוכה (RaffleResultDTO) או הודעת שגיאה</returns>
        [HttpPost("{id}/raffle")]
        [Authorize(Roles = "Manager")]
        public async Task<IActionResult> RaffleGift(int id)
        {
            try
            {
                var result = await giftBLL.RaffleGift(id);
                if (result == null)
                    return BadRequest("No confirmed shoppings available for raffle or gift not found.");
                //send email to winner could be added here
                return Ok(result);
            }
            catch (Exception ex)
            {

                return BadRequest(ex.Message);
            }
        }

        /// <summary>
        /// POST: api/gift/raffleAll
        /// ביצוע הגרלה לכל המתנות שטרם הוגרלו - זמין רק למנהלים
        /// מדלג על מתנות שכבר הוגרלו או ללא רכישות מאושרות
        /// שולח מיילים לכל הזוכים ומעדכן IsRaffled=true לכל מתנה
        /// </summary>
        /// <returns>רשימת כל הזוכים (List<RaffleResultDTO>)</returns>
        [HttpPost("raffleAll")]
        [Authorize(Roles = "Manager")]
        public async Task<IActionResult> RaffleAll()
        {
            try
            {
                var report = await giftBLL.RaffleAll();
                return Ok(report);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        //GET: api/gift/total-income
        // מחשב את סך ההכנסות מכל הרכישות המאושרות (לא טיוטות)
        // חישוב: סכום של (Quantity × CardPrice) לכל רכישה מאושרת
        //אובייקט JSON עם סכום כולל של הכנסות במטבע מקומי
        [HttpGet("total-income")]
        [Authorize(Roles = "Manager")]
        public async Task<IActionResult> GetTotalIncome()
        {
            try
            {
                var totalIncome = await giftBLL.GetTotalIncome();
                return Ok(new
                {
                    totalIncome = totalIncome,
                    message = $"Total income from all confirmed purchases: {totalIncome} ₪"
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }
    }
}


