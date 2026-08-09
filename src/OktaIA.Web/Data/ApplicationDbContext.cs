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

    // ---------- Plataforma de integração ----------
    public DbSet<Conector> Conectores => Set<Conector>();
    public DbSet<CredencialConector> CredenciaisConector => Set<CredencialConector>();
    public DbSet<CursorSync> CursoresSync => Set<CursorSync>();
    public DbSet<AlertaUnificado> AlertasUnificados => Set<AlertaUnificado>();
    public DbSet<ExecucaoSync> ExecucoesSync => Set<ExecucaoSync>();

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

        builder.Entity<Conector>(entity =>
        {
            entity.Property(c => c.TipoAuth).HasConversion<string>();
            entity.Property(c => c.Status).HasConversion<string>();
            // O mesmo fabricante só pode estar instalado uma vez por empresa — instalar duas vezes
            // duplicaria o sync e, com ele, todo alerta lido.
            entity.HasIndex(c => new { c.CompanyId, c.Slug }).IsUnique();
            entity.HasOne(c => c.Company).WithMany().HasForeignKey(c => c.CompanyId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(c => c.Credencial).WithOne(cr => cr.Conector!)
                  .HasForeignKey<CredencialConector>(cr => cr.ConectorId).OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(c => c.Cursores).WithOne(cs => cs.Conector!)
                  .HasForeignKey(cs => cs.ConectorId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<CursorSync>(entity =>
        {
            entity.Property(cs => cs.Escopo).HasConversion<string>();
            entity.HasIndex(cs => new { cs.ConectorId, cs.Escopo }).IsUnique();
        });

        builder.Entity<AlertaUnificado>(entity =>
        {
            entity.Property(a => a.Severidade).HasConversion<string>();
            // Chave de idempotência: reprocessar a mesma janela do fabricante não duplica alerta.
            // É o que permite o sync ser retomável sem medo depois de uma falha no meio.
            entity.HasIndex(a => new { a.ConectorId, a.IdExterno }).IsUnique();
            entity.HasIndex(a => new { a.CompanyId, a.OcorridoEm });
            entity.HasOne(a => a.Company).WithMany().HasForeignKey(a => a.CompanyId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(a => a.Conector).WithMany().HasForeignKey(a => a.ConectorId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<ExecucaoSync>(entity =>
        {
            entity.Property(e => e.Escopo).HasConversion<string>();
            entity.HasIndex(e => new { e.ConectorId, e.IniciadoEm });
            entity.HasOne(e => e.Conector).WithMany().HasForeignKey(e => e.ConectorId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
