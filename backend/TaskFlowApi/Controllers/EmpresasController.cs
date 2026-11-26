using Microsoft.AspNetCore.Mvc;
using MySqlConnector;

namespace TaskFlowApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class EmpresasController : ControllerBase
    {
        private readonly IConfiguration _configuration;

        public EmpresasController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        [HttpGet]
        public async Task<IActionResult> ListarEmpresas()
        {
            var empresas = new List<object>();

            var connectionString = _configuration.GetConnectionString("DefaultConnection");

            using var connection = new MySqlConnection(connectionString);
            await connection.OpenAsync();

            var query = @"
                SELECT 
                    empresas.id,
                    usuarios.nome,
                    usuarios.email,
                    usuarios.telefone,
                    empresas.cnpj,
                    empresas.nome_fantasia,
                    empresas.descricao,
                    empresas.cidade,
                    empresas.estado
                FROM empresas
                INNER JOIN usuarios ON empresas.usuario_id = usuarios.id
                WHERE usuarios.ativo = TRUE;
            ";

            using var command = new MySqlCommand(query, connection);
            using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                empresas.Add(new
                {
                    id = reader.GetInt32("id"),
                    nome = reader.GetString("nome"),
                    email = reader.GetString("email"),
                    telefone = reader.IsDBNull(reader.GetOrdinal("telefone")) ? null : reader.GetString("telefone"),
                    cnpj = reader.IsDBNull(reader.GetOrdinal("cnpj")) ? null : reader.GetString("cnpj"),
                    nomeFantasia = reader.IsDBNull(reader.GetOrdinal("nome_fantasia")) ? null : reader.GetString("nome_fantasia"),
                    descricao = reader.IsDBNull(reader.GetOrdinal("descricao")) ? null : reader.GetString("descricao"),
                    cidade = reader.IsDBNull(reader.GetOrdinal("cidade")) ? null : reader.GetString("cidade"),
                    estado = reader.IsDBNull(reader.GetOrdinal("estado")) ? null : reader.GetString("estado")
                });
            }

            return Ok(empresas);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> BuscarEmpresaPorId(int id)
        {
            var connectionString = _configuration.GetConnectionString("DefaultConnection");

            using var connection = new MySqlConnection(connectionString);
            await connection.OpenAsync();

            var query = @"
                SELECT 
                    empresas.id,
                    usuarios.nome,
                    usuarios.email,
                    usuarios.telefone,
                    empresas.cnpj,
                    empresas.nome_fantasia,
                    empresas.descricao,
                    empresas.cidade,
                    empresas.estado
                FROM empresas
                INNER JOIN usuarios ON empresas.usuario_id = usuarios.id
                WHERE empresas.id = @Id
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
                    mensagem = "Empresa não encontrada."
                });
            }

            var empresa = new
            {
                id = reader.GetInt32("id"),
                nome = reader.GetString("nome"),
                email = reader.GetString("email"),
                telefone = reader.IsDBNull(reader.GetOrdinal("telefone")) ? null : reader.GetString("telefone"),
                cnpj = reader.IsDBNull(reader.GetOrdinal("cnpj")) ? null : reader.GetString("cnpj"),
                nomeFantasia = reader.IsDBNull(reader.GetOrdinal("nome_fantasia")) ? null : reader.GetString("nome_fantasia"),
                descricao = reader.IsDBNull(reader.GetOrdinal("descricao")) ? null : reader.GetString("descricao"),
                cidade = reader.IsDBNull(reader.GetOrdinal("cidade")) ? null : reader.GetString("cidade"),
                estado = reader.IsDBNull(reader.GetOrdinal("estado")) ? null : reader.GetString("estado")
            };

            return Ok(empresa);
        }
    }
}