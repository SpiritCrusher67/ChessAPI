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

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<User>().HasData(
                new User { Id = -1, Name = "Firstuser1" },
                new User { Id = -2, Name = "Secontuser23" },
                new User { Id = -3, Name = "mainAdmin" });
            modelBuilder.Entity<User>().OwnsOne(u => u.Data).HasData(
                new UserData { UserId = -1, Login = "first1", Password = "12345", Role = "User" },
                new UserData { UserId = -2, Login = "second2", Password = "12345", Role = "User" },
                new UserData { UserId = -3, Login = "admin", Password = "admin", Role = "Admin" });

        }
    }
}
