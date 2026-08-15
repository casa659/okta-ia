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

    // ---------- Diagnóstico de segurança (assessment) ----------
    public DbSet<Diagnostico> Diagnosticos => Set<Diagnostico>();
    public DbSet<DiagnosticoResposta> DiagnosticoRespostas => Set<DiagnosticoResposta>();
    public DbSet<DiagnosticoFerramenta> DiagnosticoFerramentas => Set<DiagnosticoFerramenta>();
    public DbSet<DiagnosticoRisco> DiagnosticoRiscos => Set<DiagnosticoRisco>();
    public DbSet<DiagnosticoAcao> DiagnosticoAcoes => Set<DiagnosticoAcao>();
    public DbSet<DiagnosticoAnalise> DiagnosticoAnalises => Set<DiagnosticoAnalise>();

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

        // ---------- Diagnóstico de segurança ----------

        builder.Entity<Diagnostico>(entity =>
        {
            entity.Property(d => d.Status).HasConversion<string>();
            entity.Property(d => d.Maturidade).HasPrecision(3, 1);
            entity.HasIndex(d => new { d.CompanyId, d.CriadoEm });
            // Some junto com a empresa: diagnóstico é levantamento DELA, não tem valor solto.
            entity.HasOne(d => d.Company).WithMany().HasForeignKey(d => d.CompanyId).OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(d => d.Respostas).WithOne(r => r.Diagnostico!).HasForeignKey(r => r.DiagnosticoId).OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(d => d.Ferramentas).WithOne(f => f.Diagnostico!).HasForeignKey(f => f.DiagnosticoId).OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(d => d.Riscos).WithOne(r => r.Diagnostico!).HasForeignKey(r => r.DiagnosticoId).OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(d => d.Acoes).WithOne(a => a.Diagnostico!).HasForeignKey(a => a.DiagnosticoId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<DiagnosticoResposta>(entity =>
        {
            entity.Property(r => r.Situacao).HasConversion<string>();
            entity.Property(r => r.Origem).HasConversion<string>();
            // Uma resposta por pergunta por diagnóstico. Sem isto, salvar a mesma tela duas vezes
            // deixaria duas respostas divergentes para o mesmo controle e o cálculo escolheria uma
            // delas em silêncio.
            entity.HasIndex(r => new { r.DiagnosticoId, r.PerguntaCodigo }).IsUnique();
        });

        builder.Entity<DiagnosticoFerramenta>(entity =>
        {
            entity.HasIndex(f => new { f.DiagnosticoId, f.DominioCodigo });
        });

        builder.Entity<DiagnosticoRisco>(entity =>
        {
            entity.Property(r => r.Gravidade).HasConversion<string>();
            entity.Property(r => r.Origem).HasConversion<string>();
            entity.HasIndex(r => new { r.DiagnosticoId, r.Prioridade });
        });

        builder.Entity<DiagnosticoAcao>(entity =>
        {
            entity.Property(a => a.Horizonte).HasConversion<string>();
            entity.Property(a => a.Encaminhamento).HasConversion<string>();
            // Apagar um risco não pode apagar a ação: o plano já pode ter sido entregue ao cliente.
            entity.HasOne(a => a.Risco).WithMany().HasForeignKey(a => a.RiscoId).OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<DiagnosticoAnalise>(entity =>
        {
            entity.Property(a => a.Resultado).HasConversion<string>();
            entity.HasIndex(a => new { a.DiagnosticoId, a.GeradaEm });
            entity.HasOne(a => a.Diagnostico).WithMany().HasForeignKey(a => a.DiagnosticoId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
