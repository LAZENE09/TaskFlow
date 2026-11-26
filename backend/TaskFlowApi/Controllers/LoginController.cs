using Microsoft.AspNetCore.Mvc;
using MySqlConnector;

namespace TaskFlowApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class LoginController : ControllerBase
    {
        private readonly IConfiguration _configuration;

        public LoginController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public class LoginRequest
        {
            public string Email { get; set; } = string.Empty;
            public string Senha { get; set; } = string.Empty;
        }

        [HttpPost]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Senha))
            {
                return BadRequest(new
                {
                    mensagem = "E-mail e senha são obrigatórios."
                });
            }

            var connectionString = _configuration.GetConnectionString("DefaultConnection");

            using var connection = new MySqlConnection(connectionString);
            await connection.OpenAsync();

            var query = @"
                SELECT 
                    id,
                    nome,
                    email,
                    telefone,
                    tipo_usuario,
                    ativo
                FROM usuarios
                WHERE email = @Email
                AND senha = @Senha
                AND ativo = TRUE
                LIMIT 1;
            ";

            using var command = new MySqlCommand(query, connection);
            command.Parameters.AddWithValue("@Email", request.Email);
            command.Parameters.AddWithValue("@Senha", request.Senha);

            using var reader = await command.ExecuteReaderAsync();

            if (!await reader.ReadAsync())
            {
                return Unauthorized(new
                {
                    mensagem = "E-mail ou senha inválidos."
                });
            }

            var usuario = new
            {
                id = reader.GetInt32("id"),
                nome = reader.GetString("nome"),
                email = reader.GetString("email"),
                telefone = reader.IsDBNull(reader.GetOrdinal("telefone")) ? null : reader.GetString("telefone"),
                tipoUsuario = reader.GetString("tipo_usuario"),
                ativo = reader.GetBoolean("ativo")
            };

            return Ok(new
            {
                mensagem = "Login realizado com sucesso.",
                usuario
            });
        }

        [HttpGet("teste")]
        public async Task<IActionResult> TestarLogin([FromQuery] string email, [FromQuery] string senha)
        {
            var request = new LoginRequest
            {
                Email = email,
                Senha = senha
            };

            return await Login(request);
        }
    }
}