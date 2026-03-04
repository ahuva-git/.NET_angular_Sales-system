using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebApiProject.DAL.Interfaces;
using WebApiProject.Models.DTO;

namespace WebApiProject.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    
    public class WinnersController : ControllerBase
    {
        private readonly IRaffleWinnerDAL raffleWinnerDal;
        private readonly ILogger<WinnersController> logger;

        public WinnersController(IRaffleWinnerDAL raffleWinnerDal, ILogger<WinnersController> logger)
        {
            this.raffleWinnerDal = raffleWinnerDal;
            this.logger = logger;
        }

        /// <summary>
        /// GET: api/winners
        /// שולף את רשימת כל הזוכים מטבלת RaffleWinners
        /// כולל פרטי המתנה והמשתמש הזוכה
        /// דורש אימות (מומלץ להגביל למנהלים)
        /// </summary>
        /// <returns>רשימת זוכים כ-RaffleResultDTO</returns>
        [HttpGet]
        public async Task<IActionResult> GetAllWinners()
        {
            try
            {
                var winners = await raffleWinnerDal.GetAllAsync();
                var result = winners.Select(w => new RaffleResultDTO
                {
                    GiftId = w.GiftId,
                    GiftName = w.Gift.Name,
                    WinnerUserId = w.UserId,
                    WinnerUserName = w.User.UserName,
                    WinnerEmail = w.User.Email
                }).ToList();

                return Ok(result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error getting all winners");
                return BadRequest(ex.Message);
            }
        }

        /// <summary>
        /// GET: api/winners/gift/5
        /// שולף את הזוכה למתנה ספציפית
        /// מחזיר 404 אם המתנה לא הוגרלה או לא נמצאה
        /// </summary>
        /// <param name="giftId">מזהה המתנה</param>
        /// <returns>פרטי הזוכה או NotFound</returns>
        [HttpGet("gift/{giftId}")]
        public async Task<IActionResult> GetWinnerByGiftId(int giftId)
        {
            try
            {
                var winner = await raffleWinnerDal.GetByGiftIdAsync(giftId);
                if (winner == null)
                    return NotFound();

                var result = new RaffleResultDTO
                {
                    GiftId = winner.GiftId,
                    GiftName = winner.Gift.Name,
                    WinnerUserId = winner.UserId,
                    WinnerUserName = winner.User.UserName,
                    WinnerEmail = winner.User.Email
                };

                return Ok(result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error getting winner for gift {GiftId}", giftId);
                return BadRequest(ex.Message);
            }
        }

        /// <summary>
        /// DELETE: api/winners/gift/5
        /// מוחק זוכה מטבלת ההגרלות - זמין רק למנהלים
        /// שימושי לביטול הגרלה או תיקון טעות
        /// לא משנה את IsRaffled של המתנה - רק מוחק את הרשומה
        /// </summary>
        /// <param name="giftId">מזהה המתנה</param>
        /// <returns>הודעת הצלחה או NotFound</returns>
        [HttpDelete("gift/{giftId}")]
        [Authorize(Roles = "Manager")]
        public async Task<IActionResult> DeleteWinner(int giftId)
        {
            try
            {
                var deleted = await raffleWinnerDal.DeleteByGiftIdAsync(giftId);
                if (!deleted)
                    return NotFound($"No winner found for gift {giftId}");

                return Ok("Winner deleted successfully");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error deleting winner for gift {GiftId}", giftId);
                return BadRequest(ex.Message);
            }
        }
    }
}