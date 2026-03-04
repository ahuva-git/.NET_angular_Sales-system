using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebApiProject.BLL.Interfaces;
using WebApiProject.Models.DTO;

namespace WebApiProject.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]//א''א לעשות כלום בלי לוגין

    public class ShoppingController : ControllerBase
    {
        private readonly IShoppingBLLService shoppingBLL;
        private readonly IUserBLLService userBLL;
        private readonly IGiftBLLService giftBLL;

        public ShoppingController(IShoppingBLLService shoppingBLL, IUserBLLService userBLL, IGiftBLLService giftBLL)
        {
            this.shoppingBLL = shoppingBLL;
            this.userBLL = userBLL;
            this.giftBLL = giftBLL;
        }

        /// <summary>
        /// GET: api/shopping
        /// מנהל: רואה את כל הרכישות המאושרות (IsDraft = false)
        /// לקוח: רואה רק את הרכישות שלו (טיוטות + מאושרות)
        /// דורש אימות (Authorize)
        /// </summary>
        /// <returns>רשימת רכישות לפי תפקיד המשתמש</returns>
        [HttpGet]

        public async Task<IActionResult> Get()
        {
            try
            {
                var userId = int.Parse(User.Claims.First(c => c.Type.Contains("nameidentifier")).Value);
                var isManager = User.IsInRole("Manager");

                if (isManager)
                {
                    // מנהל רואה את כל הרכישות שאושרו
                    return Ok(await shoppingBLL.Get());
                }
                //else
                //{
                //    // לקוח רואה רק את הרכישות שלו שהן טיוטה
                //    var all = await shoppingBLL.Get();
                //    var drafts = all.Where(s => s.UserId == userId && s.IsDraft).ToList();
                //    return Ok(drafts);
                //}
                else
                {
                    // לקוח רואה את כל הרכישות שלו (טיוטות וגם שאושרו)
                    var all = await shoppingBLL.GetAll(); // 🔄 שונה מ-Get() ל-GetAll()//////////////////////
                    var userShoppings = all.Where(s => s.UserId == userId).ToList();
                    return Ok(userShoppings);  // ✅ מחזיר הכל
                }
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

  


        /// <summary>
        /// GET: api/shopping/sorted?SortBy=Date&Desc=true
        /// שולף רכישות ממוינות לפי תאריך או מחיר - זמין רק למנהלים
        /// </summary>
        /// <param name="sort">אובייקט מיון עם פרמטרים</param>
        /// <returns>רשימת רכישות ממוינות</returns>
        [HttpGet("sorted")]
        [Authorize(Roles = "Manager")]

        public async Task<IActionResult> GetSorted([FromQuery] ShoppingSortDTO sort)
        {
            try
            {
                var result = await shoppingBLL.GetSorted(sort);
                return Ok(result);
            }
              catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        /// <summary>
        /// GET: api/shopping/5
        /// שולף רכישה ספציפית לפי מזהה - זמין רק למנהלים
        /// </summary>
        /// <param name="id">מזהה הרכישה</param>
        /// <returns>פרטי הרכישה או NotFound</returns>
        [HttpGet("{id}")]
        [Authorize(Roles = "Manager")]
        public async Task<IActionResult> Get(int id)
        {
            try
            { 
                if (id <= 0)
                    return BadRequest("Invalid id");

                var shopping = await shoppingBLL.GetById(id);
                if (shopping == null)
                    return NotFound($"Shopping with id {id} does not exist.");

                return Ok(shopping);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }


        /// <summary>
        /// POST: api/shopping
        /// יוצר רכישה חדשה (טיוטה - IsDraft = true)
        /// מוודא שהמשתמש והמתנה קיימים לפני יצירת הרכישה
        /// ממלא אוטומטית פרטי משתמש מה-UserBLL
        /// </summary>
        /// <param name="createDTO">נתוני הרכישה (מזהה משתמש, מזהה מתנה, כמות)</param>
        /// <returns>הודעת הצלחה או שגיאה</returns>
        [HttpPost]
        public async Task<IActionResult> Post([FromBody] ShoppingCreateDTO createDTO)
        {
            try
            {
                var user = await userBLL.GetById(createDTO.UserId);
                if (user == null) return NotFound($"User with id {createDTO.UserId} does not exist.");

                var gift = await giftBLL.GetById(createDTO.GiftId);
                if (gift == null) return NotFound($"Gift with id {createDTO.GiftId} does not exist.");

                var shoppingDTO = new ShoppingDTO
                {
                    UserId = createDTO.UserId,
                    GiftId = createDTO.GiftId,
                    Quantity = createDTO.Quantity,
                    IsDraft = true,
                    // ✅ ממלא פרטי משתמש כאן כדי שלא יווצר null ב-BLL
                    UserName = user.UserName,
                    Email = user.Email,
                    Phone = user.Phone
                };

                await shoppingBLL.Add(shoppingDTO);

                // ✅ עכשיו shoppingDTO.Id יתעדכן אחרי השמירה ב-DB
                return Ok("Shopping created successfully.");
            }
            
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

       
        /// <summary>
        /// PUT: api/shopping/5
        /// מעדכן רכישה קיימת (רק אם היא טיוטה)
        /// לא ניתן לעדכן רכישה מאושרת (IsDraft = false)
        /// מוודא שהמשתמש והמתנה קיימים
        /// </summary>
        /// <param name="id">מזהה הרכישה לעדכון</param>
        /// <param name="shoppingDTO">נתונים מעודכנים</param>
        /// <returns>הודעת הצלחה או שגיאה</returns>
        [HttpPut("{id}")]
        public async Task<IActionResult> Put(int id, [FromBody] ShoppingDTO shoppingDTO)
        {
            //var checks = await Task.WhenAll(
            //    shoppingBLL.GetById(id),
            //    userBLL.GetById(shoppingDTO.UserId),
            //    giftBLL.GetById(shoppingDTO.GiftId)
            //);

            //if (checks[0] == null) return NotFound($"Shopping with id {id} does not exist.");
            //if (checks[1] == null) return NotFound($"User with id {shoppingDTO.UserId} does not exist.");
            //if (checks[2] == null) return NotFound($"Gift with id {shoppingDTO.GiftId} does not exist.");
            ////כך חוסכים קריאות מסודרות ומקבילות ב-DB.

            try
            {
                if (id <= 0)
                    return BadRequest("Invalid id");

                var exists = await shoppingBLL.GetById(id);
                if (exists == null)
                    return NotFound($"Shopping with id {id} does not exist.");

                var user = await userBLL.GetById(shoppingDTO.UserId);
                if (user == null)
                    return NotFound($"User with id {shoppingDTO.UserId} does not exist.");

                var gift = await giftBLL.GetById(shoppingDTO.GiftId);
                if (gift == null)
                    return NotFound($"Gift with id {shoppingDTO.GiftId} does not exist.");

                var updated = await shoppingBLL.Put(id, shoppingDTO);
                if (!updated)
                    return BadRequest("Cannot update confirmed shopping.");

                return Ok("Shopping updated successfully.");
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }


        /// <summary>
        /// DELETE: api/shopping/5
        /// מוחק רכישה (רק אם היא טיוטה)
        /// לא ניתן למחוק רכישה מאושרת (IsDraft = false)
        /// דורש אימות
        /// </summary>
        /// <param name="id">מזהה הרכישה למחיקה</param>
        /// <returns>הודעת הצלחה או שגיאה</returns>
        [HttpDelete("{id}")]
        [Authorize]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                if (id <= 0)
                    return BadRequest("Invalid id");

                var exists = await shoppingBLL.GetById(id);
                if (exists == null)
                    return NotFound($"Shopping with id {id} does not exist.");

                var deleted = await shoppingBLL.Delete(id);
                if (!deleted)
                    return BadRequest("Cannot delete confirmed shopping.");

                return Ok("Shopping deleted successfully.");
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        /// <summary>
        /// POST: api/shopping/5/confirm
        /// מאשר רכישה טיוטה - משנה IsDraft ל-false
        /// לאחר אישור, לא ניתן לעדכן או למחוק את הרכישה
        /// שקול לסגירת עגלת קניות
        /// </summary>
        /// <param name="id">מזהה הרכישה לאישור</param>
        /// <returns>הודעת הצלחה או שגיאה</returns>
        [HttpPost("{id}/confirm")]
        public async Task<IActionResult> ConfirmShopping(int id)
        {
            if (id <= 0)
                return BadRequest("Invalid id");

            try
            {
                var confirmed = await shoppingBLL.ConfirmShopping(id);
                if (!confirmed)
                    return BadRequest("Shopping could not be confirmed for unknown reason.");

                return Ok("Shopping confirmed successfully.");
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message); 
            }
        }

 
    }


}