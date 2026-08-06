using Microsoft.AspNetCore.Identity;

namespace OktaIA.Web.Models;

// Conta de analista/operador SOC (MSSP) — não é conta de cliente final; login dá acesso à
// visão consolidada de todas as empresas geridas (Grupo/Empresas), trocando de contexto pelo
// seletor de tenant no cabeçalho, não por conta separada por empresa.
public class ApplicationUser : IdentityUser
{
    public string? NomeCompleto { get; set; }
    public string? Iniciais { get; set; }
}
