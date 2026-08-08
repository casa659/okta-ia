using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using OktaIA.Web.Models;

namespace OktaIA.Web.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Company> Companies => Set<Company>();
    public DbSet<SecurityEvent> SecurityEvents => Set<SecurityEvent>();
    public DbSet<InfraHealthSnapshot> InfraHealthSnapshots => Set<InfraHealthSnapshot>();
    public DbSet<Asset> Assets => Set<Asset>();
    public DbSet<Vulnerability> Vulnerabilities => Set<Vulnerability>();
    public DbSet<ScanAlerta> ScanAlertas => Set<ScanAlerta>();
    public DbSet<Incident> Incidents => Set<Incident>();
    public DbSet<IncidentStep> IncidentSteps => Set<IncidentStep>();
    public DbSet<IncidentTimelineEvent> IncidentTimelineEvents => Set<IncidentTimelineEvent>();
    public DbSet<ContactRequest> ContactRequests => Set<ContactRequest>();
    public DbSet<ContactChannel> ContactChannels => Set<ContactChannel>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<AdminAuditLog> AdminAuditLogs => Set<AdminAuditLog>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Company>(entity =>
        {
            entity.Property(c => c.UptimePercentual).HasPrecision(5, 2);
        });

        builder.Entity<SecurityEvent>(entity =>
        {
            entity.Property(e => e.Severidade).HasConversion<string>();
            entity.Property(e => e.OrigemLat).HasPrecision(9, 6);
            entity.Property(e => e.OrigemLng).HasPrecision(9, 6);
            entity.HasIndex(e => e.CriadoEm);
            entity.HasOne(e => e.Company).WithMany().HasForeignKey(e => e.CompanyId).OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<Asset>(entity =>
        {
            entity.Property(a => a.TlsStatus).HasConversion<string>();
            entity.Property(a => a.UptimePercentual).HasPrecision(6, 3);
            entity.HasOne(a => a.Company).WithMany().HasForeignKey(a => a.CompanyId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Vulnerability>(entity =>
        {
            entity.Property(v => v.Severidade).HasConversion<string>();
            entity.Property(v => v.Cvss).HasPrecision(3, 1);
            entity.HasOne(v => v.Company).WithMany().HasForeignKey(v => v.CompanyId).OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<Incident>(entity =>
        {
            entity.Property(i => i.Severidade).HasConversion<string>();
            entity.HasMany(i => i.Passos).WithOne(p => p.Incident!).HasForeignKey(p => p.IncidentId).OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(i => i.LinhaDoTempo).WithOne(t => t.Incident!).HasForeignKey(t => t.IncidentId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(i => i.Company).WithMany().HasForeignKey(i => i.CompanyId).OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<RolePermission>(entity =>
        {
            entity.HasIndex(rp => new { rp.RoleId, rp.AreaKey }).IsUnique();
            entity.HasOne(rp => rp.Role).WithMany().HasForeignKey(rp => rp.RoleId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
