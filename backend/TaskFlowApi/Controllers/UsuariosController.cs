using Microsoft.AspNetCore.Mvc;
using MySqlConnector;

namespace TaskFlowApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UsuariosController : ControllerBase
    {
        private readonly IConfiguration _configuration;

        public UsuariosController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public class CadastroUsuarioRequest
        {
            public string Nome { get; set; } = string.Empty;
            public string Email { get; set; } = string.Empty;
            public string Senha { get; set; } = string.Empty;
            public string? Telefone { get; set; }
            public string TipoUsuario { get; set; } = string.Empty;

            public string? Cidade { get; set; }
            public string? Estado { get; set; }
            public string? Documento { get; set; }
            public string? NomeFantasia { get; set; }
            public string? Descricao { get; set; }
            public string? Experiencia { get; set; }
        }

        [HttpPost]
        public async Task<IActionResult> CadastrarUsuario([FromBody] CadastroUsuarioRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Nome))
                return BadRequest(new { mensagem = "Nome é obrigatório." });

            if (string.IsNullOrWhiteSpace(request.Email))
                return BadRequest(new { mensagem = "E-mail é obrigatório." });

            if (string.IsNullOrWhiteSpace(request.Senha))
                return BadRequest(new { mensagem = "Senha é obrigatória." });

            if (request.TipoUsuario != "freelancer" && request.TipoUsuario != "empresa")
                return BadRequest(new { mensagem = "Tipo de usuário deve ser 'freelancer' ou 'empresa'." });

            var connectionString = _configuration.GetConnectionString("DefaultConnection");

            using var connection = new MySqlConnection(connectionString);
            await connection.OpenAsync();

            using var transaction = await connection.BeginTransactionAsync();

            try
            {
                var verificarEmailQuery = @"
                    SELECT COUNT(*) 
                    FROM usuarios 
                    WHERE email = @Email;
                ";

                using (var verificarCommand = new MySqlCommand(verificarEmailQuery, connection, transaction))
                {
                    verificarCommand.Parameters.AddWithValue("@Email", request.Email);

                    var emailExiste = Convert.ToInt32(await verificarCommand.ExecuteScalarAsync());

                    if (emailExiste > 0)
                    {
                        return Conflict(new
                        {
                            mensagem = "Já existe um usuário cadastrado com este e-mail."
                        });
                    }
                }

                var inserirUsuarioQuery = @"
                    INSERT INTO usuarios 
                    (nome, email, senha, telefone, tipo_usuario)
                    VALUES
                    (@Nome, @Email, @Senha, @Telefone, @TipoUsuario);

                    SELECT LAST_INSERT_ID();
                ";

                int usuarioId;

                using (var command = new MySqlCommand(inserirUsuarioQuery, connection, transaction))
                {
                    command.Parameters.AddWithValue("@Nome", request.Nome);
                    command.Parameters.AddWithValue("@Email", request.Email);
                    command.Parameters.AddWithValue("@Senha", request.Senha);
                    command.Parameters.AddWithValue("@Telefone", string.IsNullOrWhiteSpace(request.Telefone) ? DBNull.Value : request.Telefone);
                    command.Parameters.AddWithValue("@TipoUsuario", request.TipoUsuario);

                    usuarioId = Convert.ToInt32(await command.ExecuteScalarAsync());
                }

                if (request.TipoUsuario == "freelancer")
                {
                    var inserirFreelancerQuery = @"
                        INSERT INTO freelancers
                        (usuario_id, cpf, descricao, experiencia, cidade, estado, disponibilidade)
                        VALUES
                        (@UsuarioId, @Cpf, @Descricao, @Experiencia, @Cidade, @Estado, TRUE);
                    ";

                    using var command = new MySqlCommand(inserirFreelancerQuery, connection, transaction);
                    command.Parameters.AddWithValue("@UsuarioId", usuarioId);
                    command.Parameters.AddWithValue("@Cpf", string.IsNullOrWhiteSpace(request.Documento) ? DBNull.Value : request.Documento);
                    command.Parameters.AddWithValue("@Descricao", string.IsNullOrWhiteSpace(request.Descricao) ? DBNull.Value : request.Descricao);
                    command.Parameters.AddWithValue("@Experiencia", string.IsNullOrWhiteSpace(request.Experiencia) ? DBNull.Value : request.Experiencia);
                    command.Parameters.AddWithValue("@Cidade", string.IsNullOrWhiteSpace(request.Cidade) ? DBNull.Value : request.Cidade);
                    command.Parameters.AddWithValue("@Estado", string.IsNullOrWhiteSpace(request.Estado) ? DBNull.Value : request.Estado);

                    await command.ExecuteNonQueryAsync();
                }
                else if (request.TipoUsuario == "empresa")
                {
                    var inserirEmpresaQuery = @"
                        INSERT INTO empresas
                        (usuario_id, cnpj, nome_fantasia, descricao, cidade, estado)
                        VALUES
                        (@UsuarioId, @Cnpj, @NomeFantasia, @Descricao, @Cidade, @Estado);
                    ";

                    using var command = new MySqlCommand(inserirEmpresaQuery, connection, transaction);
                    command.Parameters.AddWithValue("@UsuarioId", usuarioId);
                    command.Parameters.AddWithValue("@Cnpj", string.IsNullOrWhiteSpace(request.Documento) ? DBNull.Value : request.Documento);
                    command.Parameters.AddWithValue("@NomeFantasia", string.IsNullOrWhiteSpace(request.NomeFantasia) ? request.Nome : request.NomeFantasia);
                    command.Parameters.AddWithValue("@Descricao", string.IsNullOrWhiteSpace(request.Descricao) ? DBNull.Value : request.Descricao);
                    command.Parameters.AddWithValue("@Cidade", string.IsNullOrWhiteSpace(request.Cidade) ? DBNull.Value : request.Cidade);
                    command.Parameters.AddWithValue("@Estado", string.IsNullOrWhiteSpace(request.Estado) ? DBNull.Value : request.Estado);

                    await command.ExecuteNonQueryAsync();
                }

                await transaction.CommitAsync();

                return Created("", new
                {
                    mensagem = "Usuário cadastrado com sucesso.",
                    usuario = new
                    {
                        id = usuarioId,
                        nome = request.Nome,
                        email = request.Email,
                        telefone = request.Telefone,
                        tipoUsuario = request.TipoUsuario
                    }
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();

                return StatusCode(500, new
                {
                    mensagem = "Erro ao cadastrar usuário.",
                    erro = ex.Message
                });
            }
        }

        [HttpGet]
        public async Task<IActionResult> ListarUsuarios()
        {
            var usuarios = new List<object>();

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
                    data_criacao,
                    ativo
                FROM usuarios
                ORDER BY id DESC;
            ";

            using var command = new MySqlCommand(query, connection);
            using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                usuarios.Add(new
                {
                    id = reader.GetInt32("id"),
                    nome = reader.GetString("nome"),
                    email = reader.GetString("email"),
                    telefone = reader.IsDBNull(reader.GetOrdinal("telefone")) ? null : reader.GetString("telefone"),
                    tipoUsuario = reader.GetString("tipo_usuario"),
                    dataCriacao = reader.GetDateTime("data_criacao").ToString("yyyy-MM-dd HH:mm:ss"),
                    ativo = reader.GetBoolean("ativo")
                });
            }

            return Ok(usuarios);
        }
    }
}