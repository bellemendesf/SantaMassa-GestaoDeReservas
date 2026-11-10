using GestaoReservas.Models;
using Microsoft.Data.SqlClient;

public class AuthSqlRepository : DataConnection, IAuthRepository
{
    public AuthSqlRepository(IConfiguration configuration) : base(configuration) { }

    // Login de funcionário: e-mail institucional + senha
    public Funcionario? Login(LoginViewModel model) =>
        AutenticarFuncionario(model.Email, model.Senha);

    // Login de cliente: continua por CPF + senha
    public Cliente? LoginCliente(LoginViewModel model) =>
        AutenticarCliente(model.Cpf, model.Senha);

    // Busca o funcionário pelo e-mail institucional (sem diferenciar maiúsculas) e confere a senha
    private Funcionario? AutenticarFuncionario(string email, string senha)
    {
        // Sem e-mail informado → não autentica
        if (string.IsNullOrWhiteSpace(email))
            return null;

        using var cmd = new SqlCommand(
            @"SELECT p.id, p.nome, p.cpf, COALESCE(f.senha_hash, ''), f.email_institucional
              FROM pessoas p
              INNER JOIN funcionarios f ON p.id = f.pessoa_id
              WHERE LOWER(LTRIM(RTRIM(f.email_institucional))) = LOWER(LTRIM(RTRIM(@Email)))", conn);
        // Parâmetro do e-mail digitado no login
        cmd.Parameters.AddWithValue("@Email", email.Trim());

        // Abre a conexão com o banco
        conn.Open();
        try
        {
            // Executa a consulta
            using var reader = cmd.ExecuteReader();
            // Nenhum funcionário com esse e-mail
            if (!reader.Read())
                return null;

            // Confere o hash (ou texto legado) da senha
            if (!SenhaConfere(reader.GetString(3), senha))
                return null;

            // Monta o modelo autenticado (claims usam Id e Nome)
            return new Funcionario
            {
                Id = reader.GetInt32(0),
                Nome = reader.GetString(1),
                Cpf = reader.GetString(2),
                EmailInstitucional = reader.GetString(4)
            };
        }
        finally
        {
            // Sempre fecha a conexão
            conn.Close();
        }
    }

    private Cliente? AutenticarCliente(string cpf, string senha)
    {
        using var cmd = new SqlCommand(
            @"SELECT p.id, p.nome, p.cpf, COALESCE(c.senha_hash, ''), c.email_pessoal, c.telefone -- CORREÇÃO: senha própria do cliente
              FROM pessoas p
              INNER JOIN clientes c ON p.id = c.pessoa_id
              WHERE p.cpf = @Cpf", conn);
        cmd.Parameters.AddWithValue("@Cpf", cpf);

        conn.Open();
        try
        {
            using var reader = cmd.ExecuteReader();
            if (!reader.Read())
                return null;

            if (!SenhaConfere(reader.GetString(3), senha))
                return null;

            return new Cliente
            {
                Id = reader.GetInt32(0),
                Nome = reader.GetString(1),
                Cpf = reader.GetString(2),
                EmailPessoal = reader.GetString(4),
                Telefone = reader.GetString(5)
            };
        }
        finally
        {
            conn.Close();
        }
    }

    private static bool SenhaConfere(string armazenada, string informada) =>
        armazenada.StartsWith("AQAAAA", StringComparison.Ordinal)
            ? PasswordHasherUtil.VerifyPassword(armazenada, informada)
            : armazenada == informada;
}
