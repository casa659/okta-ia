namespace OktaIA.Web.Services;

// Idioma corrente vem de um cookie simples ("okia_lang") — sem middleware de localização do
// ASP.NET Core (a UI inteira já muda de idioma via texto, não via CultureInfo/recursos .resx;
// só os números/datas usam pt-BR fixo, como no resto deste ambiente).
public class I18nService
{
    public const string CookieName = "okia_lang";

    private readonly IHttpContextAccessor _http;

    public I18nService(IHttpContextAccessor http)
    {
        _http = http;
    }

    public string Lang
    {
        get
        {
            var valor = _http.HttpContext?.Request.Cookies[CookieName];
            return valor == "en" ? "en" : "pt";
        }
    }

    public string T(string key) => Translations.For(Lang).TryGetValue(key, out var v) ? v : key;
}
