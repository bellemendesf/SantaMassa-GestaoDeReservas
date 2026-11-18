// Utilitário: valida e-mail institucional do funcionário (domínio Santa Massa)
// O login usa ValidacaoEntrada.ErroEmailInstitucional (aceita os e-mails atuais).
// O cadastro/alteração de e-mail usa ErroEmailInstitucionalCadastro (exige @santamassa.com.br).
namespace GestaoReservas.Utilities;

// Classe estática mantida para compatibilidade; delega para ValidacaoEntrada
public static class EmailInstitucionalSantamassa
{
    // Trecho obrigatório no e-mail do login (cobre @santamassa.com e @santamassa.com.br)
    public const string TrechoDominio = ValidacaoEntrada.TrechoDominio;

    // Mensagem de domínio (quando falta @santamassa no login)
    public const string MensagemErro = ValidacaoEntrada.MensagemDominio;

    // Retorna true se o e-mail passa na regra do login (não exige .com.br)
    public static bool EhValido(string? email) =>
        ValidacaoEntrada.EmailInstitucionalEhValido(email);
}
