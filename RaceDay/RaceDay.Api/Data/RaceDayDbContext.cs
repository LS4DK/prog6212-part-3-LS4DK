using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Models;

namespace RaceDay.Api.Data;

/// <summary>
/// EF Core database context. The fluent configuration below makes the generated
/// schema match the Part 1 ERD and SQL script (names, keys, constraints).
/// </summary>
public class RaceDayDbContext : DbContext
{
    public RaceDayDbContext(DbContextOptions<RaceDayDbContext> options) : base(options) { }

    public DbSet<Role> Roles => Set<Role>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Event> Events => Set<Event>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Enrolment> Enrolments => Set<Enrolment>();
    public DbSet<Result> Results => Set<Result>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ---------- Role ----------
        modelBuilder.Entity<Role>(e =>
        {
            e.ToTable("Role");
            e.HasKey(r => r.RoleId);
            e.Property(r => r.RoleName).HasMaxLength(20).IsRequired();
            e.HasIndex(r => r.RoleName).IsUnique();

            // The two roles must exist before anyone can register
            e.HasData(
                new Role { RoleId = 1, RoleName = "Organiser" },
                new Role { RoleId = 2, RoleName = "Participant" });
        });

        // ---------- User ----------
        modelBuilder.Entity<User>(e =>
        {
            e.ToTable("User");
            e.HasKey(u => u.UserId);
            e.Property(u => u.Name).HasMaxLength(100).IsRequired();
            e.Property(u => u.Email).HasMaxLength(255).IsRequired();
            e.Property(u => u.PasswordHash).HasMaxLength(255).IsRequired();
            e.HasIndex(u => u.Email).IsUnique();

            e.HasOne(u => u.Role)
             .WithMany(r => r.Users)
             .HasForeignKey(u => u.RoleId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ---------- Event ----------
        modelBuilder.Entity<Event>(e =>
        {
            e.ToTable("Event", t =>
                t.HasCheckConstraint("CK_Event_EventType", "[EventType] IN ('Run', 'Walk', 'Cycle')"));
            e.HasKey(ev => ev.EventId);
            e.Property(ev => ev.Name).HasMaxLength(150).IsRequired();
            e.Property(ev => ev.Date).HasColumnType("datetime");
            e.Property(ev => ev.Location).HasMaxLength(150).IsRequired();
            e.Property(ev => ev.Distance).HasPrecision(6, 2);
            e.Property(ev => ev.EventType).HasConversion<string>().HasMaxLength(10);
            e.Property(ev => ev.BannerImageUrl).HasMaxLength(500);

            e.HasOne(ev => ev.Organiser)
             .WithMany(u => u.EventsOrganised)
             .HasForeignKey(ev => ev.OrganiserId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ---------- Category ----------
        modelBuilder.Entity<Category>(e =>
        {
            e.ToTable("Category");
            e.HasKey(c => c.CategoryId);
            e.Property(c => c.Name).HasMaxLength(50).IsRequired();
            e.HasIndex(c => new { c.EventId, c.Name }).IsUnique();

            e.HasOne(c => c.Event)
             .WithMany(ev => ev.Categories)
             .HasForeignKey(c => c.EventId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ---------- Enrolment ----------
        modelBuilder.Entity<Enrolment>(e =>
        {
            e.ToTable("Enrolment", t =>
                t.HasCheckConstraint("CK_Enrolment_Status", "[Status] IN ('Pending', 'Confirmed', 'Cancelled')"));
            e.HasKey(en => en.EnrolmentId);
            e.Property(en => en.EnrolmentDate).HasColumnType("datetime").HasDefaultValueSql("GETDATE()");
            e.Property(en => en.Status).HasConversion<string>().HasMaxLength(20);

            // A participant can only enrol in a given event once
            e.HasIndex(en => new { en.ParticipantId, en.EventId }).IsUnique();

            e.HasOne(en => en.Participant)
             .WithMany(u => u.Enrolments)
             .HasForeignKey(en => en.ParticipantId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(en => en.Event)
             .WithMany(ev => ev.Enrolments)
             .HasForeignKey(en => en.EventId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(en => en.Category)
             .WithMany(c => c.Enrolments)
             .HasForeignKey(en => en.CategoryId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ---------- Result ----------
        modelBuilder.Entity<Result>(e =>
        {
            e.ToTable("Result");
            e.HasKey(r => r.ResultId);

            // One-to-one (0..1): EF makes EnrolmentId unique, so an enrolment can have at most one result
            e.HasOne(r => r.Enrolment)
             .WithOne(en => en.Result)
             .HasForeignKey<Result>(r => r.EnrolmentId)
             .OnDelete(DeleteBehavior.Restrict);
        });
    }
}