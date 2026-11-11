using Xunit;
using GestaoReservas.Models;

// Testes da grade de horários (18:30–23:30) e da formatação, sem mexer no banco.
public class HorarioFuncionamentoTests
{
    [Fact]
    public void Grade_comeca_as_18h30_termina_as_23h30_de_30_em_30()
    {
        var lista = HorarioFuncionamento.Horarios;
        Assert.Equal(new TimeOnly(18, 30), lista.First());
        Assert.Equal(new TimeOnly(23, 30), lista.Last());
        Assert.Equal(11, lista.Count);
        for (var i = 1; i < lista.Count; i++)
            Assert.Equal(TimeSpan.FromMinutes(30), lista[i] - lista[i - 1]);
    }

    [Theory]
    [InlineData("18:30")]
    [InlineData("19:00")]
    [InlineData("21:30")]
    [InlineData("23:30")]
    public void Parse_aceita_horario_da_grade(string texto)
    {
        var hora = HorarioFuncionamento.Parse(texto);
        Assert.NotNull(hora);
        Assert.True(HorarioFuncionamento.EhHorarioDaGrade(hora.Value));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("17:00")]
    [InlineData("18:00")]
    [InlineData("18:45")]
    [InlineData("23:45")]
    [InlineData("24:00")]
    [InlineData("abc")]
    public void Parse_rejeita_horario_fora_da_grade(string? texto)
    {
        Assert.Null(HorarioFuncionamento.Parse(texto));
    }

    [Fact]
    public void Combinar_junta_a_data_com_o_horario()
    {
        var data = new DateTime(2027, 3, 15);
        var hora = new TimeOnly(19, 30);
        var junto = HorarioFuncionamento.Combinar(data, hora);
        Assert.Equal(new DateTime(2027, 3, 15, 19, 30, 0), junto);
        // A parte do dia continua a mesma
        Assert.Equal(data.Date, junto.Date);
    }

    [Fact]
    public void Validar_rejeita_horario_fora_do_funcionamento()
    {
        var agora = new DateTime(2027, 3, 15, 12, 0, 0);
        var data = new DateTime(2027, 3, 16, 12, 0, 0); // meio-dia, fora da grade
        var erro = HorarioFuncionamento.Validar(data, agora);
        Assert.NotNull(erro);
        Assert.Contains("18:30", erro);
    }

    [Fact]
    public void Validar_rejeita_hoje_com_horario_passado()
    {
        var agora = new DateTime(2027, 3, 15, 20, 10, 0);
        var data = new DateTime(2027, 3, 15, 19, 30, 0);
        var erro = HorarioFuncionamento.Validar(data, agora);
        Assert.NotNull(erro);
        Assert.Contains("passou", erro, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validar_aceita_hoje_com_horario_futuro()
    {
        var agora = new DateTime(2027, 3, 15, 19, 10, 0);
        var data = new DateTime(2027, 3, 15, 19, 30, 0);
        Assert.Null(HorarioFuncionamento.Validar(data, agora));
    }

    [Fact]
    public void Validar_aceita_outro_dia_mesmo_se_o_relogio_ja_passou_desse_horario()
    {
        var agora = new DateTime(2027, 3, 15, 22, 0, 0);
        var data = new DateTime(2027, 3, 16, 18, 30, 0);
        Assert.Null(HorarioFuncionamento.Validar(data, agora));
    }

    [Fact]
    public void Formatar_mostra_horario_quando_existe_e_so_a_data_quando_nao()
    {
        var comHora = new DateTime(2027, 3, 15, 19, 0, 0);
        var semHora = new DateTime(2027, 3, 15, 0, 0, 0);
        Assert.Equal("15/03/2027 às 19:00", HorarioFuncionamento.Formatar(comHora));
        Assert.Equal("15/03/2027", HorarioFuncionamento.Formatar(semHora));
        Assert.True(HorarioFuncionamento.TemHorarioDefinido(comHora));
        Assert.False(HorarioFuncionamento.TemHorarioDefinido(semHora));
    }

    [Fact]
    public void Comparacao_por_dia_ignora_o_horario()
    {
        var a = new DateTime(2027, 3, 15, 18, 30, 0);
        var b = new DateTime(2027, 3, 15, 23, 30, 0);
        var c = new DateTime(2027, 3, 16, 18, 30, 0);
        Assert.Equal(DateOnly.FromDateTime(a.Date), DateOnly.FromDateTime(b.Date));
        Assert.NotEqual(DateOnly.FromDateTime(a.Date), DateOnly.FromDateTime(c.Date));
    }
}
