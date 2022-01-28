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
                new User() { Name = "Firstuser1", Data = new UserData() { Login = "first1", Password = "12345", Role = "User" } },
                new User() { Name = "Secontuser23", Data = new UserData() { Login = "second2", Password = "12345", Role = "User" } },
                new User() { Name = "mainAdmin", Data = new UserData() { Login = "admin", Password = "admin", Role = "Admin" } });

        }
    }
}
