using Microsoft.AspNetCore.Mvc;
using MySqlConnector;

namespace TaskFlowApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CategoriasController : ControllerBase
    {
        private readonly IConfiguration _configuration;

        public CategoriasController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        [HttpGet]
        public async Task<IActionResult> ListarCategorias()
        {
            var categorias = new List<object>();

            var connectionString = _configuration.GetConnectionString("DefaultConnection");

            using var connection = new MySqlConnection(connectionString);
            await connection.OpenAsync();

            var query = @"
                SELECT 
                    id,
                    nome,
                    descricao
                FROM categorias
                ORDER BY nome ASC;
            ";

            using var command = new MySqlCommand(query, connection);
            using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                categorias.Add(new
                {
                    id = reader.GetInt32("id"),
                    nome = reader.GetString("nome"),
                    descricao = reader.IsDBNull(reader.GetOrdinal("descricao")) ? null : reader.GetString("descricao")
                });
            }

            return Ok(categorias);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> BuscarCategoriaPorId(int id)
        {
            var connectionString = _configuration.GetConnectionString("DefaultConnection");

            using var connection = new MySqlConnection(connectionString);
            await connection.OpenAsync();

            var query = @"
                SELECT 
                    id,
                    nome,
                    descricao
                FROM categorias
                WHERE id = @Id
                LIMIT 1;
            ";

            using var command = new MySqlCommand(query, connection);
            command.Parameters.AddWithValue("@Id", id);

            using var reader = await command.ExecuteReaderAsync();

            if (!await reader.ReadAsync())
            {
                return NotFound(new
                {
                    mensagem = "Categoria não encontrada."
                });
            }

            var categoria = new
            {
                id = reader.GetInt32("id"),
                nome = reader.GetString("nome"),
                descricao = reader.IsDBNull(reader.GetOrdinal("descricao")) ? null : reader.GetString("descricao")
            };

            return Ok(categoria);
        }
    }
}