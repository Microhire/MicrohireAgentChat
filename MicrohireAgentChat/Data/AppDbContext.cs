using MicrohireAgentChat.Models;
using Microsoft.EntityFrameworkCore;

namespace MicrohireAgentChat.Data
{
    public sealed class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options) { }

        public DbSet<AgentThread> AgentThreads { get; set; } = null!;
        public DbSet<WestinLead> WestinLeads { get; set; } = null!;

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
                e.Property(x => x.Email).HasMaxLength(200);
                e.Property(x => x.DraftStateJson); // nvarchar(max) for the JSON blob
                e.Property(x => x.CreatedUtc).IsRequired();
                e.Property(x => x.LastSeenUtc).IsRequired();

                e.HasIndex(x => x.Email); // non-unique: multiple threads per email is allowed (lead links create fresh ones)
            });

            modelBuilder.Entity<WestinLead>(e =>
            {
                e.ToTable("WestinLeads", "dbo");
                e.HasKey(x => x.Id);

                e.HasIndex(x => x.Token).IsUnique();

                e.Property(x => x.Token).IsRequired();
                e.Property(x => x.Organisation).HasMaxLength(200).IsRequired();
                e.Property(x => x.OrganisationAddress).HasMaxLength(500).IsRequired();
                e.Property(x => x.FirstName).HasMaxLength(100).IsRequired();
                e.Property(x => x.LastName).HasMaxLength(100).IsRequired();
                e.Property(x => x.Email).HasMaxLength(200).IsRequired();
                e.Property(x => x.PhoneNumber).HasMaxLength(50).IsRequired();
                e.Property(x => x.EventStartDate).HasMaxLength(20).IsRequired();
                e.Property(x => x.EventEndDate).HasMaxLength(20).IsRequired();
                e.Property(x => x.Venue).HasMaxLength(100).IsRequired();
                e.Property(x => x.Room).HasMaxLength(100).IsRequired();
                e.Property(x => x.Attendees).HasMaxLength(20).IsRequired();
                e.Property(x => x.CreatedUtc).IsRequired();
                e.Property(x => x.BookingNo).HasMaxLength(35);
                e.Property(x => x.QuoteSignedUtc);
                e.Property(x => x.SignedByName).HasMaxLength(200);
            });
        }
    }
}
