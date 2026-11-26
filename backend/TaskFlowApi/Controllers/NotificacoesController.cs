using Microsoft.AspNetCore.Mvc;
using MySqlConnector;

namespace TaskFlowApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class NotificacoesController : ControllerBase
    {
        private readonly IConfiguration _configuration;

        public NotificacoesController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        [HttpGet]
        public async Task<IActionResult> ListarNotificacoes()
        {
            var notificacoes = new List<object>();

            var connectionString = _configuration.GetConnectionString("DefaultConnection");

            using var connection = new MySqlConnection(connectionString);
            await connection.OpenAsync();

            var query = @"
                SELECT 
                    notificacoes.id,
                    usuarios.nome AS usuario,
                    notificacoes.titulo,
                    notificacoes.mensagem,
                    notificacoes.tipo,
                    notificacoes.lida,
                    notificacoes.data_criacao
                FROM notificacoes
                INNER JOIN usuarios ON notificacoes.usuario_id = usuarios.id
                ORDER BY notificacoes.data_criacao DESC;
            ";

            using var command = new MySqlCommand(query, connection);
            using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                notificacoes.Add(new
                {
                    id = reader.GetInt32("id"),
                    usuario = reader.GetString("usuario"),
                    titulo = reader.GetString("titulo"),
                    mensagem = reader.GetString("mensagem"),
                    tipo = reader.GetString("tipo"),
                    lida = reader.GetBoolean("lida"),
                    dataCriacao = reader.GetDateTime("data_criacao").ToString("yyyy-MM-dd HH:mm:ss")
                });
            }

            return Ok(notificacoes);
        }

        [HttpGet("usuario/{usuarioId}")]
        public async Task<IActionResult> ListarPorUsuario(int usuarioId)
        {
            var notificacoes = new List<object>();

            var connectionString = _configuration.GetConnectionString("DefaultConnection");

            using var connection = new MySqlConnection(connectionString);
            await connection.OpenAsync();

            var query = @"
                SELECT 
                    id,
                    titulo,
                    mensagem,
                    tipo,
                    lida,
                    data_criacao
                FROM notificacoes
                WHERE usuario_id = @UsuarioId
                ORDER BY data_criacao DESC;
            ";

            using var command = new MySqlCommand(query, connection);
            command.Parameters.AddWithValue("@UsuarioId", usuarioId);

            using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                notificacoes.Add(new
                {
                    id = reader.GetInt32("id"),
                    titulo = reader.GetString("titulo"),
                    mensagem = reader.GetString("mensagem"),
                    tipo = reader.GetString("tipo"),
                    lida = reader.GetBoolean("lida"),
                    dataCriacao = reader.GetDateTime("data_criacao").ToString("yyyy-MM-dd HH:mm:ss")
                });
            }

            return Ok(notificacoes);
        }

        [HttpPut("{id}/marcar-como-lida")]
        public async Task<IActionResult> MarcarComoLida(int id)
        {
            var connectionString = _configuration.GetConnectionString("DefaultConnection");

            using var connection = new MySqlConnection(connectionString);
            await connection.OpenAsync();

            var query = @"
                UPDATE notificacoes
                SET lida = TRUE
                WHERE id = @Id;
            ";

            using var command = new MySqlCommand(query, connection);
            command.Parameters.AddWithValue("@Id", id);

            var linhasAfetadas = await command.ExecuteNonQueryAsync();

            if (linhasAfetadas == 0)
            {
                return NotFound(new
                {
                    mensagem = "Notificação não encontrada."
                });
            }

            return Ok(new
            {
                mensagem = "Notificação marcada como lida com sucesso."
            });
        }
    }
}