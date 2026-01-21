using MicrohireAgentChat.Models;
using Microsoft.EntityFrameworkCore;

namespace MicrohireAgentChat.Data
{
    public sealed class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options) { }

        public DbSet<AgentThread> AgentThreads { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<AgentThread>(e =>
            {
                e.ToTable("AgentThreads", "dbo");
                e.HasKey(x => x.Id);

                e.HasIndex(x => x.UserKey).IsUnique();
                e.HasIndex(x => x.ThreadId).IsUnique();

                e.Property(x => x.UserKey).HasMaxLength(200).IsRequired();
                e.Property(x => x.ThreadId).HasMaxLength(200).IsRequired();
                e.Property(x => x.CreatedUtc).IsRequired();
                e.Property(x => x.LastSeenUtc).IsRequired();
            });
        }
    }
}
