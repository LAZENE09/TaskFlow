using Microsoft.AspNetCore.Mvc;
using MySqlConnector;

namespace TaskFlowApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MensagensController : ControllerBase
    {
        private readonly IConfiguration _configuration;

        public MensagensController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        [HttpGet("contrato/{contratoId}")]
        public async Task<IActionResult> ListarPorContrato(int contratoId)
        {
            var mensagens = new List<object>();

            var connectionString = _configuration.GetConnectionString("DefaultConnection");

            using var connection = new MySqlConnection(connectionString);
            await connection.OpenAsync();

            var query = @"
                SELECT 
                    mensagens.id,
                    mensagens.contrato_id,
                    remetente.nome AS remetente,
                    destinatario.nome AS destinatario,
                    mensagens.mensagem,
                    mensagens.lida,
                    mensagens.data_envio
                FROM mensagens
                INNER JOIN usuarios AS remetente ON mensagens.remetente_id = remetente.id
                INNER JOIN usuarios AS destinatario ON mensagens.destinatario_id = destinatario.id
                WHERE mensagens.contrato_id = @ContratoId
                ORDER BY mensagens.data_envio ASC;
            ";

            using var command = new MySqlCommand(query, connection);
            command.Parameters.AddWithValue("@ContratoId", contratoId);

            using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                mensagens.Add(new
                {
                    id = reader.GetInt32("id"),
                    contratoId = reader.GetInt32("contrato_id"),
                    remetente = reader.GetString("remetente"),
                    destinatario = reader.GetString("destinatario"),
                    mensagem = reader.GetString("mensagem"),
                    lida = reader.GetBoolean("lida"),
                    dataEnvio = reader.GetDateTime("data_envio").ToString("yyyy-MM-dd HH:mm:ss")
                });
            }

            return Ok(mensagens);
        }

        [HttpGet]
        public async Task<IActionResult> ListarTodas()
        {
            var mensagens = new List<object>();

            var connectionString = _configuration.GetConnectionString("DefaultConnection");

            using var connection = new MySqlConnection(connectionString);
            await connection.OpenAsync();

            var query = @"
                SELECT 
                    mensagens.id,
                    mensagens.contrato_id,
                    remetente.nome AS remetente,
                    destinatario.nome AS destinatario,
                    mensagens.mensagem,
                    mensagens.lida,
                    mensagens.data_envio
                FROM mensagens
                INNER JOIN usuarios AS remetente ON mensagens.remetente_id = remetente.id
                INNER JOIN usuarios AS destinatario ON mensagens.destinatario_id = destinatario.id
                ORDER BY mensagens.data_envio ASC;
            ";

            using var command = new MySqlCommand(query, connection);
            using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                mensagens.Add(new
                {
                    id = reader.GetInt32("id"),
                    contratoId = reader.GetInt32("contrato_id"),
                    remetente = reader.GetString("remetente"),
                    destinatario = reader.GetString("destinatario"),
                    mensagem = reader.GetString("mensagem"),
                    lida = reader.GetBoolean("lida"),
                    dataEnvio = reader.GetDateTime("data_envio").ToString("yyyy-MM-dd HH:mm:ss")
                });
            }

            return Ok(mensagens);
        }

        public class CriarMensagemRequest
        {
            public int ContratoId { get; set; }
            public int RemetenteId { get; set; }
            public int DestinatarioId { get; set; }
            public string Mensagem { get; set; } = string.Empty;
        }

        [HttpPost]
        public async Task<IActionResult> EnviarMensagem([FromBody] CriarMensagemRequest request)
        {
            if (request.ContratoId <= 0)
                return BadRequest(new { mensagem = "Contrato é obrigatório." });

            if (request.RemetenteId <= 0)
                return BadRequest(new { mensagem = "Remetente é obrigatório." });

            if (request.DestinatarioId <= 0)
                return BadRequest(new { mensagem = "Destinatário é obrigatório." });

            if (string.IsNullOrWhiteSpace(request.Mensagem))
                return BadRequest(new { mensagem = "A mensagem não pode estar vazia." });

            var connectionString = _configuration.GetConnectionString("DefaultConnection");

            using var connection = new MySqlConnection(connectionString);
            await connection.OpenAsync();

            var query = @"
        INSERT INTO mensagens
        (
            contrato_id,
            remetente_id,
            destinatario_id,
            mensagem,
            lida
        )
        VALUES
        (
            @ContratoId,
            @RemetenteId,
            @DestinatarioId,
            @Mensagem,
            FALSE
        );

        SELECT LAST_INSERT_ID();
    ";

            using var command = new MySqlCommand(query, connection);

            command.Parameters.AddWithValue("@ContratoId", request.ContratoId);
            command.Parameters.AddWithValue("@RemetenteId", request.RemetenteId);
            command.Parameters.AddWithValue("@DestinatarioId", request.DestinatarioId);
            command.Parameters.AddWithValue("@Mensagem", request.Mensagem);

            var mensagemId = Convert.ToInt32(await command.ExecuteScalarAsync());

            return Created("", new
            {
                mensagem = "Mensagem enviada com sucesso.",
                dados = new
                {
                    id = mensagemId,
                    contratoId = request.ContratoId,
                    remetenteId = request.RemetenteId,
                    destinatarioId = request.DestinatarioId,
                    texto = request.Mensagem,
                    lida = false
                }
            });
        }
    }
}