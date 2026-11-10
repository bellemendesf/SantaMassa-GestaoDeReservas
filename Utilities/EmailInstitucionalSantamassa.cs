// Utilitário: valida e-mail institucional do funcionário (domínio Santa Massa)
namespace GestaoReservas.Utilities;

// Classe estática com a regra única de domínio institucional
public static class EmailInstitucionalSantamassa
{
    // Trecho obrigatório no e-mail (cobre @santamassa.com e @santamassa.com.br do script.sql)
    public const string TrechoDominio = "@santamassa";

    // Mensagem de erro em português para login/cadastro/edição
    public const string MensagemErro =
        "O e-mail institucional deve conter @santamassa (ex.: nome@santamassa.com.br).";

    // Retorna true se o e-mail contém @santamassa (sem distinguir maiúsculas)
    public static bool EhValido(string? email)
    {
        // Sem e-mail → inválido
        if (string.IsNullOrWhiteSpace(email)) return false;
        // Aceita qualquer domínio que contenha o trecho @santamassa
        return email.Contains(TrechoDominio, StringComparison.OrdinalIgnoreCase);
    }
}
