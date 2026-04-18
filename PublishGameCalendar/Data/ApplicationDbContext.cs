using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PublishGameCalendar.Domain;
using PublishGameCalendar.Identity;

namespace PublishGameCalendar.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    public DbSet<Series> Series { get; set; }
    public DbSet<Subscription> Subscriptions { get; set; }
    public DbSet<PollingConfig> PollingConfigs { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Series>().ToTable("series");

        builder.Entity<Subscription>()
            .ToTable("subscriptions")
            .HasKey(s => new { s.UserId, s.SeriesId });

        builder.Entity<PollingConfig>()
            .ToTable("polling_config")
            .HasKey(p => p.SeriesId);
    }
}