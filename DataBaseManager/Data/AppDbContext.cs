using DataBaseManager.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataBaseManager.Data
{
    public class AppDbContext :DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<Price> prices { get; set; }
        public DbSet<Stock> stocks { get; set; }
        public DbSet<Provider> providers { get; set; }
        public DbSet<Sector> sectors { get; set; }
        public DbSet<Fundamentals> fundamentals { get; set; }
        public DbSet<Industries> industries { get; set; }
        public DbSet<Indicators> indicators { get; set; }
        public DbSet<News> news { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                var connectionString = Environment.GetEnvironmentVariable("SqlConnectionString");
                optionsBuilder.UseSqlServer(connectionString);
            }

        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Fundamentals>()
             .Property(f => f.earnings)
             .HasPrecision(18, 4);

            modelBuilder.Entity<Indicators>()
            .Property(f => f.sma)
            .HasPrecision(18, 4);

            modelBuilder.Entity<Indicators>()
           .Property(f => f.rsi)
           .HasPrecision(18, 4);

            modelBuilder.Entity<Indicators>()
           .Property(f => f.macd)
           .HasPrecision(18, 4);

            modelBuilder.Entity<News>()
           .Property(f => f.sentiment)
           .HasPrecision(18, 4);

            modelBuilder.Entity<Price>()
           .Property(f => f.open)
           .HasPrecision(18, 4);

            modelBuilder.Entity<Price>()
          .Property(f => f.low)
          .HasPrecision(18, 4);

            modelBuilder.Entity<Price>()
          .Property(f => f.close)
          .HasPrecision(18, 4);

            modelBuilder.Entity<Price>()
          .Property(f => f.high)
          .HasPrecision(18, 4);

        }
    }
}
