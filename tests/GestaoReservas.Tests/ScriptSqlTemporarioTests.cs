using System.Diagnostics;
using System.Text;
using Microsoft.Data.SqlClient;
using Xunit;

namespace GestaoReservas.Tests;

// Banco TEMPORÁRIO gestaoReserva_t38u_tmp, criado a partir de uma CÓPIA do script.sql
// com o nome do banco trocado. Nunca altera gestaoReserva nem os outros bancos existentes.
public class ScriptSqlTemporarioTests : IAsyncLifetime
{
    public const string BancoTemp = "gestaoReserva_t38u_tmp";
    public const string BancoReal = "gestaoReserva";
    private const string MasterCs = "Data Source=localhost;Initial Catalog=master;Integrated Security=True;TrustServerCertificate=True;";
    public const string TempCs = "Data Source=localhost;Initial Catalog=" + BancoTemp + ";Integrated Security=True;TrustServerCertificate=True;";

    private string? _pastaTemp;
    private bool _servidorOk;
    private string? _erroConexao;

    public async Task InitializeAsync()
    {
        _pastaTemp = Path.Combine(Path.GetTempPath(), "t38u-sql-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_pastaTemp);
        try
        {
            await using var cn = new SqlConnection(MasterCs);
            await cn.OpenAsync();
            await using var cmd = new SqlCommand("SELECT @@SERVERNAME", cn);
            await cmd.ExecuteScalarAsync();
            _servidorOk = true;
        }
        catch (Exception ex)
        {
            _servidorOk = false;
            _erroConexao = ex.Message;
        }
    }

    public async Task DisposeAsync()
    {
        if (_servidorOk)
            await DroparBancoTempSeExistir();
        if (_pastaTemp != null && Directory.Exists(_pastaTemp))
        {
            try { Directory.Delete(_pastaTemp, true); } catch { /* temp */ }
        }
    }

    [Fact]
    public async Task ScriptDoZeroEViewsNnEmBancoTemporario()
    {
        if (!_servidorOk)
        {
            Assert.Fail("Não foi possível conectar ao SQL Server de localhost só para o banco temporário. " + _erroConexao);
            return;
        }

        await GarantirQueBancoRealNaoSeraAlvo();
        var scriptOriginal = LocalizarScriptSql();
        Assert.True(File.Exists(scriptOriginal), "script.sql não encontrado para a cópia temporária.");

        var textoOriginal = File.ReadAllText(scriptOriginal, Encoding.GetEncoding(1252));
        Assert.Contains("status 0 = Pendente", textoOriginal);
        Assert.Contains("data_hora guarda somente a data", textoOriginal);

        var scriptRenomeado = Path.Combine(_pastaTemp!, "script_tmp_zero.sql");
        var textoTemp = RenomearBanco(textoOriginal);
        Assert.DoesNotContain("db_id('gestaoReserva')", textoTemp, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(BancoTemp, textoTemp);
        File.WriteAllText(scriptRenomeado, textoTemp, Encoding.GetEncoding(1252));

        await DroparBancoTempSeExistir();
        var execZero = ExecutarSqlcmd(scriptRenomeado);

        // Os títulos sem '--' NÃO quebram o script (sqlcmd/SQL Server os ignora ou o lote segue).
        // O script do zero ainda falha mais abaixo, no EXEC buscCli, por e-mail unique.
        Assert.False(execZero.Ok, "O script do zero deveria falhar, mas concluiu com sucesso. Saída:\n" + execZero.Saida);
        Assert.True(
            execZero.Saida.Contains("uq_clientes_email", StringComparison.OrdinalIgnoreCase)
            || execZero.Saida.Contains("maria@email.com", StringComparison.OrdinalIgnoreCase)
            || execZero.Saida.Contains("buscCli", StringComparison.OrdinalIgnoreCase),
            "Esperado falha no EXEC buscCli (e-mail duplicado). Saída:\n" + execZero.Saida);
        Assert.DoesNotContain("Incorrect syntax", execZero.Saida, StringComparison.OrdinalIgnoreCase);

        await GarantirQueBancoRealNaoSeraAlvo();
        await using var master = new SqlConnection(MasterCs);
        await master.OpenAsync();
        await using var existe = new SqlCommand($"SELECT COUNT(*) FROM sys.databases WHERE name = '{BancoTemp}'", master);
        Assert.Equal(1, Convert.ToInt32(await existe.ExecuteScalarAsync()));

        await using var cn = new SqlConnection(TempCs);
        await cn.OpenAsync();

        async Task<int> Escalar(string sql)
        {
            await using var cmd = new SqlCommand(sql, cn);
            var v = await cmd.ExecuteScalarAsync();
            return Convert.ToInt32(v);
        }

        Assert.True(await Escalar("SELECT COUNT(*) FROM v_funcionarios") >= 3, "v_funcionarios");
        Assert.True(await Escalar("SELECT COUNT(*) FROM v_clientes") >= 3, "v_clientes");
        Assert.True(await Escalar("SELECT COUNT(*) FROM v_ambientes") >= 3, "v_ambientes");
        Assert.True(await Escalar("SELECT COUNT(*) FROM v_mesas") >= 1, "v_mesas");
        Assert.True(await Escalar("SELECT COUNT(*) FROM v_reservas") >= 3, "v_reservas");
        Assert.True(await Escalar("SELECT COUNT(*) FROM v_reservas_mesas") >= 6, "v_reservas_mesas N:N");
        Assert.True(await Escalar("SELECT COUNT(*) FROM v_reservas_detalhadas") >= 6, "v_reservas_detalhadas N:N");

        var bloquearMesa = await Tentar("DELETE FROM mesas WHERE nr = 1");
        Assert.False(bloquearMesa.Ok, "Excluir mesa com reserva deveria falhar no SQL por FK. " + bloquearMesa.Erro);
        Assert.Equal(1, await Escalar("SELECT COUNT(*) FROM mesas WHERE nr = 1"));

        var bloquearCliente = await Tentar("DELETE FROM clientes WHERE pessoa_id = 1");
        Assert.False(bloquearCliente.Ok, "Excluir cliente com reservas deveria falhar no SQL por FK. " + bloquearCliente.Erro);
        Assert.Equal(1, await Escalar("SELECT COUNT(*) FROM clientes WHERE pessoa_id = 1"));

        var cpfDup = await Tentar("INSERT INTO pessoas (nome, cpf) VALUES (N'Duplicado', '999.888.777-66')");
        Assert.False(cpfDup.Ok, "CPF duplicado deveria falhar no SQL. " + cpfDup.Erro);

        await DroparBancoTempSeExistir();
        await using var check = new SqlCommand($"SELECT COUNT(*) FROM sys.databases WHERE name = '{BancoTemp}'", master);
        Assert.Equal(0, Convert.ToInt32(await check.ExecuteScalarAsync()));
        await GarantirQueBancoRealNaoSeraAlvo();

        async Task<(bool Ok, string Erro)> Tentar(string sql)
        {
            try
            {
                await using var cmd = new SqlCommand(sql, cn);
                await cmd.ExecuteNonQueryAsync();
                return (true, "");
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }
    }

    private static string RenomearBanco(string texto) =>
        texto.Replace(BancoReal, BancoTemp, StringComparison.Ordinal);

    private static string LocalizarScriptSql()
    {
        var candidatos = new List<string>();
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (int i = 0; i < 8 && dir != null; i++, dir = dir.Parent!)
            candidatos.Add(Path.Combine(dir.FullName, "script.sql"));
        candidatos.Add(@"C:\Users\isabe\GestaoReservas_BUILD_TMP\script.sql");
        candidatos.Add(@"C:\Users\isabe\GestaoReservas\script.sql");
        return candidatos.First(File.Exists);
    }

    private async Task GarantirQueBancoRealNaoSeraAlvo()
    {
        await using var cn = new SqlConnection(MasterCs);
        await cn.OpenAsync();
        await using var cmd = new SqlCommand($"SELECT COUNT(*) FROM sys.databases WHERE name = '{BancoReal}'", cn);
        var n = Convert.ToInt32(await cmd.ExecuteScalarAsync());
        Assert.True(n == 1, $"O banco real '{BancoReal}' deveria continuar existindo (encontrado={n}).");
    }

    private async Task DroparBancoTempSeExistir()
    {
        await using var cn = new SqlConnection(MasterCs);
        await cn.OpenAsync();
        await using var cmd = new SqlCommand($@"
IF DB_ID(N'{BancoTemp}') IS NOT NULL
BEGIN
    ALTER DATABASE [{BancoTemp}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE [{BancoTemp}];
END
", cn);
        await cmd.ExecuteNonQueryAsync();
    }

    private static (bool Ok, string Saida) ExecutarSqlcmd(string arquivo)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "sqlcmd",
            Arguments = $"-S localhost -E -C -b -i \"{arquivo}\" -f 1252",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        using var p = Process.Start(psi)!;
        var saida = p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd();
        p.WaitForExit(120_000);
        return (p.ExitCode == 0, saida);
    }
}
