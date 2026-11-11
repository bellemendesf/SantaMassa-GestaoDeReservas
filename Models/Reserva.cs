// Importa os atributos de validação ([Required], [Range]...)
using System.ComponentModel.DataAnnotations;

// Define o namespace dos modelos
namespace GestaoReservas.Models;

// Modelo Reserva
public class Reserva
{
    // Propriedade Id
    public int Id { get; set; }
    // Propriedade Cliente
    public Cliente Cliente { get; set; } = new Cliente();
    // Propriedade ClienteId
    public int ClienteId { get; set; }
    // N:N: mesas escolhidas na reserva (tabela Reservas_Mesas). O cliente pode escolher mais de uma.
    public List<int> MesaIds { get; set; } = new();
    // Texto de cada mesa para as telas (ex.: "Mesa 3 · Interno · 4 lugares")
    public List<string> MesasDescricao { get; set; } = new();
    // Propriedade DataHora (coluna datetime: guarda a data e o horário escolhido)
    public DateTime DataHora { get; set; }
    // Só o formulário de criação: o valor é gravado em DataHora. Não é coluna extra no banco.
    public string? Horario { get; set; }
    // Validação: valor mínimo/máximo permitido
    [Range(1, 30, ErrorMessage = "Informe entre 1 e 30 pessoas.")]
    // Propriedade QtdPessoas
    public int QtdPessoas { get; set; }
    // Propriedade Status
    public string Status { get; set; } = "";
}
