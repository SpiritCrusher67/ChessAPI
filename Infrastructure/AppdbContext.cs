using ChessAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace ChessAPI.Infrastructure
{
    public class AppdbContext : DbContext
    {
        public virtual DbSet<User> Users { get; set; }
        public AppdbContext(DbContextOptions<AppdbContext> options): base(options)
        {
            Database.EnsureCreated();
        }


    }
}
