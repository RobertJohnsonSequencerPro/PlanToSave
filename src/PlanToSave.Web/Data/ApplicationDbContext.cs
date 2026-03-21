using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PlanToSave.Domain.Entities;

namespace PlanToSave.Web.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<BalanceSnapshot> BalanceSnapshots => Set<BalanceSnapshot>();
    public DbSet<FlowTemplate> FlowTemplates => Set<FlowTemplate>();
    public DbSet<MonthlyPlan> MonthlyPlans => Set<MonthlyPlan>();
    public DbSet<PlannedFlow> PlannedFlows => Set<PlannedFlow>();
    public DbSet<ActualFlow> ActualFlows => Set<ActualFlow>();
    public DbSet<Goal> Goals => Set<Goal>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.HasDefaultSchema("plantosave");

        base.OnModelCreating(builder);

        // ── Accounts ──────────────────────────────────────────────────
        builder.Entity<Account>(e =>
        {
            e.HasKey(a => a.Id);
            e.Property(a => a.Name).HasMaxLength(100).IsRequired();
            e.Property(a => a.Description).HasMaxLength(500);
            e.Property(a => a.Type).HasConversion<string>();
            e.HasIndex(a => a.UserId);
            e.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(a => a.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ── FlowTemplates ─────────────────────────────────────────────
        builder.Entity<FlowTemplate>(e =>
        {
            e.HasKey(t => t.Id);
            e.Property(t => t.Description).HasMaxLength(200).IsRequired();
            e.Property(t => t.Amount).HasPrecision(18, 2);
            e.HasIndex(t => t.UserId);
            e.HasOne(t => t.FromAccount).WithMany().HasForeignKey(t => t.FromAccountId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(t => t.ToAccount).WithMany().HasForeignKey(t => t.ToAccountId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(t => t.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ── MonthlyPlans ──────────────────────────────────────────────
        builder.Entity<MonthlyPlan>(e =>
        {
            e.HasKey(p => p.Id);
            e.Property(p => p.Status).HasConversion<string>();
            e.HasIndex(p => new { p.UserId, p.Year, p.Month }).IsUnique();
            e.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(p => p.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ── PlannedFlows ──────────────────────────────────────────────
        builder.Entity<PlannedFlow>(e =>
        {
            e.HasKey(f => f.Id);
            e.Property(f => f.Description).HasMaxLength(300);
            e.Property(f => f.Amount).HasPrecision(18, 2);
            e.HasOne(f => f.MonthlyPlan).WithMany(p => p.PlannedFlows)
                .HasForeignKey(f => f.MonthlyPlanId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(f => f.FromAccount).WithMany().HasForeignKey(f => f.FromAccountId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(f => f.ToAccount).WithMany().HasForeignKey(f => f.ToAccountId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(f => f.Template).WithMany().HasForeignKey(f => f.TemplateId)
                .IsRequired(false).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(f => f.Goal).WithMany(g => g.ContributionFlows).HasForeignKey(f => f.GoalId)
                .IsRequired(false).OnDelete(DeleteBehavior.SetNull);
        });

        // ── ActualFlows ───────────────────────────────────────────────
        builder.Entity<ActualFlow>(e =>
        {
            e.HasKey(f => f.Id);
            e.Property(f => f.Description).HasMaxLength(300);
            e.Property(f => f.Amount).HasPrecision(18, 2);
            e.HasIndex(f => f.UserId);
            e.HasIndex(f => f.Date);
            e.HasOne(f => f.FromAccount).WithMany().HasForeignKey(f => f.FromAccountId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(f => f.ToAccount).WithMany().HasForeignKey(f => f.ToAccountId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(f => f.PlannedFlow).WithMany().HasForeignKey(f => f.PlannedFlowId)
                .IsRequired(false).OnDelete(DeleteBehavior.SetNull);
            e.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(f => f.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ── Goals ─────────────────────────────────────────────────────
        builder.Entity<Goal>(e =>
        {
            e.HasKey(g => g.Id);
            e.Property(g => g.Name).HasMaxLength(100).IsRequired();
            e.Property(g => g.Description).HasMaxLength(500);
            e.Property(g => g.TargetAmount).HasPrecision(18, 2);
            e.HasIndex(g => g.UserId);
            e.HasOne(g => g.TargetAccount).WithMany().HasForeignKey(g => g.TargetAccountId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(g => g.SourceAccount).WithMany().HasForeignKey(g => g.SourceAccountId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(g => g.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ── BalanceSnapshots ──────────────────────────────────────────
        builder.Entity<BalanceSnapshot>(e =>
        {
            e.HasKey(s => s.Id);
            e.Property(s => s.Amount).HasPrecision(18, 2);
            e.Property(s => s.Note).HasMaxLength(300);
            e.HasIndex(s => new { s.UserId, s.AccountId });
            e.HasOne(s => s.Account).WithMany().HasForeignKey(s => s.AccountId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(s => s.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
