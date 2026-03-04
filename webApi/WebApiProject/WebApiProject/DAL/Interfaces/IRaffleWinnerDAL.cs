using WebApiProject.Models;

namespace WebApiProject.DAL.Interfaces
{
    public interface IRaffleWinnerDAL
    {
        Task<List<RaffleWinner>> GetAllAsync();
        Task<RaffleWinner?> GetByGiftIdAsync(int giftId);
        Task AddAsync(RaffleWinner winner);
        Task<bool> DeleteByGiftIdAsync(int giftId);
        IQueryable<RaffleWinner> Query();
    }
}
