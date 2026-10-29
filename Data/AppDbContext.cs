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
        public DbSet<ActivityLog> ActivityLogs { get; set; }

        // Per-user and always writable, unlike the install folder (Program Files),
        // which normal users can't write to.
        public static readonly string DataFolder =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BatchBaker");

        private static readonly string DatabasePath = PrepareDatabasePath();

        // Older versions kept the database in a data folder next to the exe. If this
        // user has no database yet, that one is copied over so its history is kept.
        private static string PrepareDatabasePath()
        {
            Directory.CreateDirectory(DataFolder);
            string path = Path.Combine(DataFolder, "batchbaker.db");

            string oldPath = Path.Combine(AppContext.BaseDirectory, "data", "batchbaker.db");
            if (!File.Exists(path) && File.Exists(oldPath))
            {
                // SQLite may keep recent changes in -wal/-shm side files, so copy those too.
                string[] suffixes = { "", "-wal", "-shm" };
                try
                {
                    foreach (string suffix in suffixes)
                    {
                        if (File.Exists(oldPath + suffix))
                        {
                            File.Copy(oldPath + suffix, path + suffix);
                        }
                    }
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    // A half-copied database is worse than none: start fresh instead.
                    foreach (string suffix in suffixes)
                    {
                        File.Delete(path + suffix);
                    }
                }
            }
            return path;
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            var connectionString = $"Data Source={DatabasePath}";
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
