// Regras de validação de entrada compartilhadas (login e cadastro)
using System.Text.RegularExpressions;

namespace GestaoReservas.Utilities;

// Centraliza a regra do e-mail institucional do funcionário
public static class ValidacaoEntrada
{
    // Trecho obrigatório no e-mail do login (cobre @santamassa.com e @santamassa.com.br)
    public const string TrechoDominio = "@santamassa";

    // Sufixo exigido no cadastro e na alteração do e-mail do funcionário
    public const string SufixoCadastro = "@santamassa.com.br";

    // Mensagem quando falta o domínio institucional (mesma do login)
    public const string MensagemDominio =
        "O e-mail institucional deve conter @santamassa (ex.: nome@santamassa.com.br).";

    // Mensagem quando o formato é inválido (pontos consecutivos no domínio, como no type=email do login)
    public const string MensagemFormato =
        "Email inválido: use apenas um ponto antes do .com, ex.: nome@santamassa.com";

    // Mesma regra de formato, com exemplo .com.br, usada só no cadastro/alteração
    public const string MensagemFormatoCadastro =
        "Email inválido: use apenas um ponto antes do .com, ex.: nome@santamassa.com.br";

    // Mensagem quando o e-mail de cadastro não termina em @santamassa.com.br
    public const string MensagemSufixoCadastro =
        "O email do funcionário deve terminar em @santamassa.com.br (ex.: nome@santamassa.com.br).";

    // Mesmo formato do type=email do HTML5 (WHATWG), usado no login de funcionário
    private static readonly Regex EmailNoFormatoHtml5 = new(
        @"^[a-zA-Z0-9.!#$%&'*+/=?^_`{|}~-]+@[a-zA-Z0-9](?:[a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?(?:\.[a-zA-Z0-9](?:[a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?)*$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    // Indica se o domínio está em formato inválido (pontos consecutivos, começa/termina com ponto, ou fora do HTML5)
    private static bool DominioEmFormatoInvalido(string valor)
    {
        var arroba = valor.LastIndexOf('@');
        var dominio = arroba >= 0 ? valor[(arroba + 1)..] : "";
        return dominio.Contains("..", StringComparison.Ordinal)
            || dominio.StartsWith('.')
            || dominio.EndsWith('.')
            || !EmailNoFormatoHtml5.IsMatch(valor);
    }

    // Devolve a mensagem de erro, ou null se o e-mail institucional for aceito no login
    public static string? ErroEmailInstitucional(string? email)
    {
        // Sem e-mail → mesma mensagem de domínio já usada no login
        if (string.IsNullOrWhiteSpace(email)) return MensagemDominio;

        var valor = email.Trim();

        if (DominioEmFormatoInvalido(valor))
        {
            // Se parece um e-mail da casa com typo no domínio, mostra a mensagem de formato
            if (valor.Contains("santamassa", StringComparison.OrdinalIgnoreCase))
                return MensagemFormato;
            // Caso contrário, mantém a mensagem de domínio já usada no login
            return MensagemDominio;
        }

        // Exige o trecho @santamassa (sem distinguir maiúsculas) — login continua aceitando .com e .com.br
        if (!valor.Contains(TrechoDominio, StringComparison.OrdinalIgnoreCase))
            return MensagemDominio;

        return null;
    }

    // Cadastro e alteração de e-mail: mantém formato/pontos e exige terminar em @santamassa.com.br
    public static string? ErroEmailInstitucionalCadastro(string? email)
    {
        if (string.IsNullOrWhiteSpace(email)) return MensagemSufixoCadastro;

        var valor = email.Trim();

        if (DominioEmFormatoInvalido(valor))
        {
            if (valor.Contains("santamassa", StringComparison.OrdinalIgnoreCase))
                return MensagemFormatoCadastro;
            return MensagemSufixoCadastro;
        }

        // Sem distinguir maiúsculas: precisa terminar exatamente em @santamassa.com.br
        if (!valor.EndsWith(SufixoCadastro, StringComparison.OrdinalIgnoreCase))
            return MensagemSufixoCadastro;

        return null;
    }

    // Na edição, a regra do sufixo .com.br só vale se o e-mail institucional tiver sido alterado
    public static bool EmailInstitucionalFoiAlterado(string? informado, string? atual) =>
        !string.Equals((informado ?? "").Trim(), (atual ?? "").Trim(), StringComparison.OrdinalIgnoreCase);

    // Retorna true se o e-mail institucional é aceito no login
    public static bool EmailInstitucionalEhValido(string? email) =>
        ErroEmailInstitucional(email) is null;

    // Mensagem quando o e-mail genérico (cliente, qualquer domínio) está em formato inválido
    public const string MensagemEmailGenerico =
        "Informe um e-mail válido (ex.: nome@email.com).";

    // Devolve a mensagem de erro, ou null se o e-mail genérico for válido
    // Exige algo@dominio.ext, recusa ".." no domínio e não exige @santamassa
    public static string? ErroEmailGenerico(string? email, string? mensagemVazio = null)
    {
        // Sem e-mail → mensagem de campo vazio (o cadastro do cliente usa "Informe o e-mail pessoal.")
        if (string.IsNullOrWhiteSpace(email))
            return string.IsNullOrWhiteSpace(mensagemVazio) ? "Informe o e-mail." : mensagemVazio;

        var valor = email.Trim();
        // Localiza o @ para separar o domínio
        var arroba = valor.LastIndexOf('@');
        // Parte depois do @ (vazia se não houver @)
        var dominio = arroba >= 0 ? valor[(arroba + 1)..] : "";

        // Recusa sem @, domínio sem ponto (ex.: a@b), ".." e o restante do formato HTML5
        if (arroba <= 0
            || DominioEmFormatoInvalido(valor)
            || !dominio.Contains('.'))
            return MensagemEmailGenerico;

        return null;
    }

    // Retorna true se o e-mail genérico está em formato válido
    public static bool EmailGenericoEhValido(string? email) =>
        ErroEmailGenerico(email) is null;
}
