using Microsoft.EntityFrameworkCore;
using WebApiProject.Models;
using WebApiProject.DAL.Interfaces;
using WebApiProject.Data;

namespace WebApiProject.DAL
{
    public class RaffleWinnerDAL: IRaffleWinnerDAL

    {
        private readonly AppDbContext context;

        public RaffleWinnerDAL(AppDbContext context)
        {
            this.context = context;
        }

        public async Task<List<RaffleWinner>> GetAllAsync()
        {
            return await context.RaffleWinners
                .Include(w => w.Gift)
                .Include(w => w.User)
                .ToListAsync();
        }

        public async Task<RaffleWinner?> GetByGiftIdAsync(int giftId)
        {
            return await context.RaffleWinners
                .Include(w => w.Gift)
                .Include(w => w.User)
                .FirstOrDefaultAsync(w => w.GiftId == giftId);
        }

        public async Task AddAsync(RaffleWinner winner)
        {
            context.RaffleWinners.Add(winner);
            await context.SaveChangesAsync();
        }

        public async Task<bool> DeleteByGiftIdAsync(int giftId)
        {
            var winner = await context.RaffleWinners.FirstOrDefaultAsync(w => w.GiftId == giftId);
            if (winner == null) return false;

            context.RaffleWinners.Remove(winner);
            await context.SaveChangesAsync();
            return true;
        }

        public IQueryable<RaffleWinner> Query()
        {
            return context.RaffleWinners;
        }
    }
}

