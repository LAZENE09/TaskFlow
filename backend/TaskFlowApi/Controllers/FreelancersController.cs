using Microsoft.AspNetCore.Mvc;
using MySqlConnector;

namespace TaskFlowApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class FreelancersController : ControllerBase
    {
        private readonly IConfiguration _configuration;

        public FreelancersController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        [HttpGet]
        public async Task<IActionResult> ListarFreelancers()
        {
            var freelancers = new List<object>();

            var connectionString = _configuration.GetConnectionString("DefaultConnection");

            using var connection = new MySqlConnection(connectionString);
            await connection.OpenAsync();

            var query = @"
                SELECT 
                    freelancers.id,
                    usuarios.nome,
                    usuarios.email,
                    usuarios.telefone,
                    freelancers.cpf,
                    freelancers.descricao,
                    freelancers.experiencia,
                    freelancers.cidade,
                    freelancers.estado,
                    freelancers.disponibilidade
                FROM freelancers
                INNER JOIN usuarios ON freelancers.usuario_id = usuarios.id
                WHERE usuarios.ativo = TRUE;
            ";

            using var command = new MySqlCommand(query, connection);
            using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                freelancers.Add(new
                {
                    id = reader.GetInt32("id"),
                    nome = reader.GetString("nome"),
                    email = reader.GetString("email"),
                    telefone = reader.IsDBNull(reader.GetOrdinal("telefone")) ? null : reader.GetString("telefone"),
                    cpf = reader.IsDBNull(reader.GetOrdinal("cpf")) ? null : reader.GetString("cpf"),
                    descricao = reader.IsDBNull(reader.GetOrdinal("descricao")) ? null : reader.GetString("descricao"),
                    experiencia = reader.IsDBNull(reader.GetOrdinal("experiencia")) ? null : reader.GetString("experiencia"),
                    cidade = reader.IsDBNull(reader.GetOrdinal("cidade")) ? null : reader.GetString("cidade"),
                    estado = reader.IsDBNull(reader.GetOrdinal("estado")) ? null : reader.GetString("estado"),
                    disponibilidade = reader.GetBoolean("disponibilidade")
                });
            }

            return Ok(freelancers);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> BuscarFreelancerPorId(int id)
        {
            var connectionString = _configuration.GetConnectionString("DefaultConnection");

            using var connection = new MySqlConnection(connectionString);
            await connection.OpenAsync();

            var query = @"
                SELECT 
                    freelancers.id,
                    usuarios.nome,
                    usuarios.email,
                    usuarios.telefone,
                    freelancers.cpf,
                    freelancers.descricao,
                    freelancers.experiencia,
                    freelancers.cidade,
                    freelancers.estado,
                    freelancers.disponibilidade
                FROM freelancers
                INNER JOIN usuarios ON freelancers.usuario_id = usuarios.id
                WHERE freelancers.id = @Id
                AND usuarios.ativo = TRUE
                LIMIT 1;
            ";

            using var command = new MySqlCommand(query, connection);
            command.Parameters.AddWithValue("@Id", id);

            using var reader = await command.ExecuteReaderAsync();

            if (!await reader.ReadAsync())
            {
                return NotFound(new
                {
                    mensagem = "Freelancer não encontrado."
                });
            }

            var freelancer = new
            {
                id = reader.GetInt32("id"),
                nome = reader.GetString("nome"),
                email = reader.GetString("email"),
                telefone = reader.IsDBNull(reader.GetOrdinal("telefone")) ? null : reader.GetString("telefone"),
                cpf = reader.IsDBNull(reader.GetOrdinal("cpf")) ? null : reader.GetString("cpf"),
                descricao = reader.IsDBNull(reader.GetOrdinal("descricao")) ? null : reader.GetString("descricao"),
                experiencia = reader.IsDBNull(reader.GetOrdinal("experiencia")) ? null : reader.GetString("experiencia"),
                cidade = reader.IsDBNull(reader.GetOrdinal("cidade")) ? null : reader.GetString("cidade"),
                estado = reader.IsDBNull(reader.GetOrdinal("estado")) ? null : reader.GetString("estado"),
                disponibilidade = reader.GetBoolean("disponibilidade")
            };

            return Ok(freelancer);
        }
    }
}