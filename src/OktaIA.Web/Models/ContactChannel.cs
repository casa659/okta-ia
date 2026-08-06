namespace OktaIA.Web.Models;

// Canal de contato exibido em /Contato (COMERCIAL/TELEFONE/WHATSAPP/ENDEREÇO no design original) —
// editável por Admin direto na página pública, não é mais hardcoded em MarketingContent.cs.
public class ContactChannel
{
    public int Id { get; set; }

    public required string Chave { get; set; }     // rótulo curto, ex. "COMERCIAL"
    public required string Cor { get; set; }        // hex, ex. "#4D9BFF"
    public required string Valor { get; set; }       // ex. "info@loktaia.com"
    public required string Descricao { get; set; }   // ex. "Propostas, demonstrações e parcerias"
    public int Ordem { get; set; }
}
