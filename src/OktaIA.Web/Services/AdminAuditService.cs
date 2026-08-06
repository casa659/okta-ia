using Microsoft.AspNetCore.Http;
using OktaIA.Web.Data;
using OktaIA.Web.Models;

namespace OktaIA.Web.Services;

public class AdminAuditService
{
    private readonly ApplicationDbContext _db;
    private readonly IHttpContextAccessor _http;

    public AdminAuditService(ApplicationDbContext db, IHttpContextAccessor http)
    {
        _db = db;
        _http = http;
    }

    public async Task RegistrarAsync(string acao, string detalhe, string autor)
    {
        _db.AdminAuditLogs.Add(new AdminAuditLog
        {
            Acao = acao,
            Detalhe = detalhe,
            Autor = autor,
            OrigemIp = _http.HttpContext?.Connection.RemoteIpAddress?.ToString(),
        });
        await _db.SaveChangesAsync();
    }
}
