using Microsoft.EntityFrameworkCore;
using PremierLeaguePredictions.Core.Entities;

namespace PremierLeaguePredictions.Infrastructure.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users { get; set; }
    public DbSet<Season> Seasons { get; set; }
    public DbSet<Team> Teams { get; set; }
    public DbSet<Gameweek> Gameweeks { get; set; }
    public DbSet<Fixture> Fixtures { get; set; }
    public DbSet<Pick> Picks { get; set; }
    public DbSet<TeamSelection> TeamSelections { get; set; }
    public DbSet<EmailNotification> EmailNotifications { get; set; }
    public DbSet<AdminAction> AdminActions { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // User configuration
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").HasDefaultValueSql("uuid_generate_v4()");
            entity.Property(e => e.Email).HasColumnName("email").HasMaxLength(255).IsRequired();
            entity.Property(e => e.FirstName).HasColumnName("first_name").HasMaxLength(100).IsRequired();
            entity.Property(e => e.LastName).HasColumnName("last_name").HasMaxLength(100).IsRequired();
            entity.Property(e => e.PhoneNumber).HasColumnName("phone_number").HasMaxLength(20);
            entity.Property(e => e.PhotoUrl).HasColumnName("photo_url").HasMaxLength(500);
            entity.Property(e => e.GoogleId).HasColumnName("google_id").HasMaxLength(255);
            entity.Property(e => e.IsActive).HasColumnName("is_active").HasDefaultValue(true);
            entity.Property(e => e.IsAdmin).HasColumnName("is_admin").HasDefaultValue(false);
            entity.Property(e => e.IsPaid).HasColumnName("is_paid").HasDefaultValue(false);
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.HasIndex(e => e.Email).IsUnique();
            entity.HasIndex(e => e.GoogleId).IsUnique();
        });

        // Season configuration
        modelBuilder.Entity<Season>(entity =>
        {
            entity.ToTable("seasons");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").HasDefaultValueSql("uuid_generate_v4()");
            entity.Property(e => e.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
            entity.Property(e => e.StartDate).HasColumnName("start_date").IsRequired();
            entity.Property(e => e.EndDate).HasColumnName("end_date").IsRequired();
            entity.Property(e => e.IsActive).HasColumnName("is_active").HasDefaultValue(false);
            entity.Property(e => e.IsArchived).HasColumnName("is_archived").HasDefaultValue(false);
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        // Team configuration
        modelBuilder.Entity<Team>(entity =>
        {
            entity.ToTable("teams");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").HasDefaultValueSql("uuid_generate_v4()");
            entity.Property(e => e.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
            entity.Property(e => e.ShortName).HasColumnName("short_name").HasMaxLength(50);
            entity.Property(e => e.Code).HasColumnName("code").HasMaxLength(10);
            entity.Property(e => e.LogoUrl).HasColumnName("logo_url").HasMaxLength(500);
            entity.Property(e => e.ExternalId).HasColumnName("external_id");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.HasIndex(e => e.ExternalId).IsUnique();
        });

        // Gameweek configuration
        modelBuilder.Entity<Gameweek>(entity =>
        {
            entity.ToTable("gameweeks");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").HasDefaultValueSql("uuid_generate_v4()");
            entity.Property(e => e.SeasonId).HasColumnName("season_id").IsRequired();
            entity.Property(e => e.WeekNumber).HasColumnName("week_number").IsRequired();
            entity.Property(e => e.Deadline).HasColumnName("deadline").IsRequired();
            entity.Property(e => e.IsLocked).HasColumnName("is_locked").HasDefaultValue(false);
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.HasOne(e => e.Season)
                .WithMany(s => s.Gameweeks)
                .HasForeignKey(e => e.SeasonId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => new { e.SeasonId, e.WeekNumber }).IsUnique();
        });

        // Fixture configuration
        modelBuilder.Entity<Fixture>(entity =>
        {
            entity.ToTable("fixtures");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").HasDefaultValueSql("uuid_generate_v4()");
            entity.Property(e => e.GameweekId).HasColumnName("gameweek_id").IsRequired();
            entity.Property(e => e.HomeTeamId).HasColumnName("home_team_id").IsRequired();
            entity.Property(e => e.AwayTeamId).HasColumnName("away_team_id").IsRequired();
            entity.Property(e => e.KickoffTime).HasColumnName("kickoff_time").IsRequired();
            entity.Property(e => e.HomeScore).HasColumnName("home_score");
            entity.Property(e => e.AwayScore).HasColumnName("away_score");
            entity.Property(e => e.Status).HasColumnName("status").HasMaxLength(50).HasDefaultValue("SCHEDULED");
            entity.Property(e => e.ExternalId).HasColumnName("external_id");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.HasOne(e => e.Gameweek)
                .WithMany(g => g.Fixtures)
                .HasForeignKey(e => e.GameweekId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.HomeTeam)
                .WithMany(t => t.HomeFixtures)
                .HasForeignKey(e => e.HomeTeamId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.AwayTeam)
                .WithMany(t => t.AwayFixtures)
                .HasForeignKey(e => e.AwayTeamId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => e.ExternalId).IsUnique();
        });

        // Pick configuration
        modelBuilder.Entity<Pick>(entity =>
        {
            entity.ToTable("picks");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").HasDefaultValueSql("uuid_generate_v4()");
            entity.Property(e => e.UserId).HasColumnName("user_id").IsRequired();
            entity.Property(e => e.GameweekId).HasColumnName("gameweek_id").IsRequired();
            entity.Property(e => e.TeamId).HasColumnName("team_id").IsRequired();
            entity.Property(e => e.Points).HasColumnName("points").HasDefaultValue(0);
            entity.Property(e => e.GoalsFor).HasColumnName("goals_for").HasDefaultValue(0);
            entity.Property(e => e.GoalsAgainst).HasColumnName("goals_against").HasDefaultValue(0);
            entity.Property(e => e.IsAutoAssigned).HasColumnName("is_auto_assigned").HasDefaultValue(false);
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.HasOne(e => e.User)
                .WithMany(u => u.Picks)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Gameweek)
                .WithMany(g => g.Picks)
                .HasForeignKey(e => e.GameweekId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Team)
                .WithMany(t => t.Picks)
                .HasForeignKey(e => e.TeamId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => new { e.UserId, e.GameweekId }).IsUnique();
        });

        // TeamSelection configuration
        modelBuilder.Entity<TeamSelection>(entity =>
        {
            entity.ToTable("team_selections");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").HasDefaultValueSql("uuid_generate_v4()");
            entity.Property(e => e.UserId).HasColumnName("user_id").IsRequired();
            entity.Property(e => e.SeasonId).HasColumnName("season_id").IsRequired();
            entity.Property(e => e.TeamId).HasColumnName("team_id").IsRequired();
            entity.Property(e => e.Half).HasColumnName("half").IsRequired();
            entity.Property(e => e.GameweekNumber).HasColumnName("gameweek_number").IsRequired();
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.HasOne(e => e.User)
                .WithMany(u => u.TeamSelections)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Season)
                .WithMany(s => s.TeamSelections)
                .HasForeignKey(e => e.SeasonId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Team)
                .WithMany(t => t.TeamSelections)
                .HasForeignKey(e => e.TeamId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => new { e.UserId, e.SeasonId, e.TeamId, e.Half }).IsUnique();
        });

        // EmailNotification configuration
        modelBuilder.Entity<EmailNotification>(entity =>
        {
            entity.ToTable("email_notifications");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").HasDefaultValueSql("uuid_generate_v4()");
            entity.Property(e => e.UserId).HasColumnName("user_id").IsRequired();
            entity.Property(e => e.GameweekId).HasColumnName("gameweek_id").IsRequired();
            entity.Property(e => e.EmailType).HasColumnName("email_type").HasMaxLength(50).IsRequired();
            entity.Property(e => e.SentAt).HasColumnName("sent_at").HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.Status).HasColumnName("status").HasMaxLength(50).HasDefaultValue("SENT");
            entity.Property(e => e.ErrorMessage).HasColumnName("error_message");

            entity.HasOne(e => e.User)
                .WithMany(u => u.EmailNotifications)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Gameweek)
                .WithMany(g => g.EmailNotifications)
                .HasForeignKey(e => e.GameweekId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // AdminAction configuration
        modelBuilder.Entity<AdminAction>(entity =>
        {
            entity.ToTable("admin_actions");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").HasDefaultValueSql("uuid_generate_v4()");
            entity.Property(e => e.AdminUserId).HasColumnName("admin_user_id").IsRequired();
            entity.Property(e => e.ActionType).HasColumnName("action_type").HasMaxLength(100).IsRequired();
            entity.Property(e => e.TargetUserId).HasColumnName("target_user_id");
            entity.Property(e => e.TargetGameweekId).HasColumnName("target_gameweek_id");
            entity.Property(e => e.Details).HasColumnName("details");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.HasOne(e => e.AdminUser)
                .WithMany(u => u.AdminActionsPerformed)
                .HasForeignKey(e => e.AdminUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.TargetUser)
                .WithMany(u => u.AdminActionsReceived)
                .HasForeignKey(e => e.TargetUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.TargetGameweek)
                .WithMany(g => g.AdminActions)
                .HasForeignKey(e => e.TargetGameweekId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
