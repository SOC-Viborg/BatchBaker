using BatchBaker.Models;
using Microsoft.EntityFrameworkCore;
using System.IO;
using System.Reflection;

namespace BatchBaker.Data
{
    public class AppDbContext : DbContext
    {
        public DbSet<ImportSet> ImportSets { get; set; }
        public DbSet<People> People { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            string path = Path.Combine(AppContext.BaseDirectory, "batchbaker1.db");
            var connectionString = $"Data Source={path}";
            optionsBuilder.UseSqlite(connectionString, options => { 
                options.MigrationsAssembly(Assembly.GetExecutingAssembly().FullName);
            });
        }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<People>()
                .HasOne(p => p.ImportSet)
                .WithMany(i => i.People)
                .HasForeignKey(p => p.ImportSetId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
