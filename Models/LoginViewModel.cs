namespace GestaoReservas.Models;

public class LoginViewModel
{
    // Usado no login de cliente (CPF + senha)
    public string Cpf { get; set; } = "";
    // Senha (cliente e funcionário)
    public string Senha { get; set; } = "";
    // Usado no login de funcionário (e-mail institucional + senha)
    public string Email { get; set; } = "";
}
