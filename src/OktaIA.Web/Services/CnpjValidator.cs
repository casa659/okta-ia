namespace OktaIA.Web.Services;

/// <summary>Validação real de CNPJ (dígitos verificadores, módulo 11) — não é só formato.</summary>
public static class CnpjValidator
{
    public static bool IsValid(string? cnpj)
    {
        if (string.IsNullOrWhiteSpace(cnpj))
        {
            return false;
        }

        var digits = new string(cnpj.Where(char.IsDigit).ToArray());
        if (digits.Length != 14)
        {
            return false;
        }

        // Sequências tipo "00000000000000" passam no cálculo do dígito verificador mas não são
        // CNPJ válido de verdade.
        if (digits.Distinct().Count() == 1)
        {
            return false;
        }

        var numbers = digits.Select(c => c - '0').ToArray();

        int CalcularDigito(int[] nums, int[] pesos)
        {
            var soma = 0;
            for (var i = 0; i < pesos.Length; i++)
            {
                soma += nums[i] * pesos[i];
            }

            var resto = soma % 11;
            return resto < 2 ? 0 : 11 - resto;
        }

        var pesos1 = new[] { 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2 };
        var digito1 = CalcularDigito(numbers[..12], pesos1);
        if (digito1 != numbers[12])
        {
            return false;
        }

        var pesos2 = new[] { 6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2 };
        var digito2 = CalcularDigito(numbers[..13], pesos2);
        return digito2 == numbers[13];
    }

    public static string Formatar(string cnpj)
    {
        var digits = new string(cnpj.Where(char.IsDigit).ToArray());
        return digits.Length == 14
            ? $"{digits[..2]}.{digits[2..5]}.{digits[5..8]}/{digits[8..12]}-{digits[12..]}"
            : cnpj;
    }
}
