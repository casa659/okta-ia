namespace OktaIA.Web.Models;

/// <summary>Pedido de demonstração enviado pelo formulário público de Contato.</summary>
public class ContactRequest
{
    public int Id { get; set; }
    public string NomeCompleto { get; set; } = "";
    public string Email { get; set; } = "";
    public string Empresa { get; set; } = "";
    public string Telefone { get; set; } = "";
    public string QuantidadeAtivos { get; set; } = "";
    public string Mensagem { get; set; } = "";
    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;
}
