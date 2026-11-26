using Microsoft.AspNetCore.Mvc;
using MySqlConnector;

namespace TaskFlowApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ContratosController : ControllerBase
    {
        private readonly IConfiguration _configuration;

        public ContratosController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        [HttpGet]
        public async Task<IActionResult> ListarContratos()
        {
            var contratos = new List<object>();

            var connectionString = _configuration.GetConnectionString("DefaultConnection");

            using var connection = new MySqlConnection(connectionString);
            await connection.OpenAsync();

            var query = @"
                SELECT 
                    contratos.id,
                    contratos.chamado_id,
                    chamados.titulo AS chamado,
                    empresas.nome_fantasia AS empresa,
                    usuarios.nome AS freelancer,
                    contratos.valor_final,
                    contratos.status,
                    contratos.data_inicio,
                    contratos.data_finalizacao
                FROM contratos
                INNER JOIN chamados ON contratos.chamado_id = chamados.id
                INNER JOIN empresas ON contratos.empresa_id = empresas.id
                INNER JOIN freelancers ON contratos.freelancer_id = freelancers.id
                INNER JOIN usuarios ON freelancers.usuario_id = usuarios.id
                ORDER BY contratos.data_inicio DESC;
            ";

            using var command = new MySqlCommand(query, connection);
            using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                contratos.Add(new
                {
                    id = reader.GetInt32("id"),
                    chamadoId = reader.GetInt32("chamado_id"),
                    chamado = reader.GetString("chamado"),
                    empresa = reader.GetString("empresa"),
                    freelancer = reader.GetString("freelancer"),
                    valorFinal = reader.IsDBNull(reader.GetOrdinal("valor_final"))
                        ? 0
                        : reader.GetDecimal("valor_final"),
                    status = reader.GetString("status"),
                    dataInicio = reader.GetDateTime("data_inicio").ToString("yyyy-MM-dd HH:mm:ss"),
                    dataFinalizacao = reader.IsDBNull(reader.GetOrdinal("data_finalizacao"))
                        ? null
                        : reader.GetDateTime("data_finalizacao").ToString("yyyy-MM-dd HH:mm:ss")
                });
            }

            return Ok(contratos);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> BuscarContratoPorId(int id)
        {
            var connectionString = _configuration.GetConnectionString("DefaultConnection");

            using var connection = new MySqlConnection(connectionString);
            await connection.OpenAsync();

            var query = @"
                SELECT 
                    contratos.id,
                    contratos.chamado_id,
                    chamados.titulo AS chamado,
                    empresas.nome_fantasia AS empresa,
                    usuarios.nome AS freelancer,
                    contratos.valor_final,
                    contratos.status,
                    contratos.data_inicio,
                    contratos.data_finalizacao
                FROM contratos
                INNER JOIN chamados ON contratos.chamado_id = chamados.id
                INNER JOIN empresas ON contratos.empresa_id = empresas.id
                INNER JOIN freelancers ON contratos.freelancer_id = freelancers.id
                INNER JOIN usuarios ON freelancers.usuario_id = usuarios.id
                WHERE contratos.id = @Id
                LIMIT 1;
            ";

            using var command = new MySqlCommand(query, connection);
            command.Parameters.AddWithValue("@Id", id);

            using var reader = await command.ExecuteReaderAsync();

            if (!await reader.ReadAsync())
            {
                return NotFound(new
                {
                    mensagem = "Contrato não encontrado."
                });
            }

            var contrato = new
            {
                id = reader.GetInt32("id"),
                chamadoId = reader.GetInt32("chamado_id"),
                chamado = reader.GetString("chamado"),
                empresa = reader.GetString("empresa"),
                freelancer = reader.GetString("freelancer"),
                valorFinal = reader.IsDBNull(reader.GetOrdinal("valor_final"))
                    ? 0
                    : reader.GetDecimal("valor_final"),
                status = reader.GetString("status"),
                dataInicio = reader.GetDateTime("data_inicio").ToString("yyyy-MM-dd HH:mm:ss"),
                dataFinalizacao = reader.IsDBNull(reader.GetOrdinal("data_finalizacao"))
                    ? null
                    : reader.GetDateTime("data_finalizacao").ToString("yyyy-MM-dd HH:mm:ss")
            };

            return Ok(contrato);
        }

        [HttpGet("freelancer/{freelancerId}")]
        public async Task<IActionResult> ListarPorFreelancer(int freelancerId)
        {
            var contratos = new List<object>();

            var connectionString = _configuration.GetConnectionString("DefaultConnection");

            using var connection = new MySqlConnection(connectionString);
            await connection.OpenAsync();

            var query = @"
                SELECT 
                    contratos.id,
                    chamados.titulo AS chamado,
                    empresas.nome_fantasia AS empresa,
                    contratos.valor_final,
                    contratos.status,
                    contratos.data_inicio,
                    contratos.data_finalizacao
                FROM contratos
                INNER JOIN chamados ON contratos.chamado_id = chamados.id
                INNER JOIN empresas ON contratos.empresa_id = empresas.id
                WHERE contratos.freelancer_id = @FreelancerId
                ORDER BY contratos.data_inicio DESC;
            ";

            using var command = new MySqlCommand(query, connection);
            command.Parameters.AddWithValue("@FreelancerId", freelancerId);

            using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                contratos.Add(new
                {
                    id = reader.GetInt32("id"),
                    chamado = reader.GetString("chamado"),
                    empresa = reader.GetString("empresa"),
                    valorFinal = reader.IsDBNull(reader.GetOrdinal("valor_final"))
                        ? 0
                        : reader.GetDecimal("valor_final"),
                    status = reader.GetString("status"),
                    dataInicio = reader.GetDateTime("data_inicio").ToString("yyyy-MM-dd HH:mm:ss"),
                    dataFinalizacao = reader.IsDBNull(reader.GetOrdinal("data_finalizacao"))
                        ? null
                        : reader.GetDateTime("data_finalizacao").ToString("yyyy-MM-dd HH:mm:ss")
                });
            }

            return Ok(contratos);
        }

        [HttpGet("empresa/{empresaId}")]
        public async Task<IActionResult> ListarPorEmpresa(int empresaId)
        {
            var contratos = new List<object>();

            var connectionString = _configuration.GetConnectionString("DefaultConnection");

            using var connection = new MySqlConnection(connectionString);
            await connection.OpenAsync();

            var query = @"
                SELECT 
                    contratos.id,
                    chamados.titulo AS chamado,
                    usuarios.nome AS freelancer,
                    contratos.valor_final,
                    contratos.status,
                    contratos.data_inicio,
                    contratos.data_finalizacao
                FROM contratos
                INNER JOIN chamados ON contratos.chamado_id = chamados.id
                INNER JOIN freelancers ON contratos.freelancer_id = freelancers.id
                INNER JOIN usuarios ON freelancers.usuario_id = usuarios.id
                WHERE contratos.empresa_id = @EmpresaId
                ORDER BY contratos.data_inicio DESC;
            ";

            using var command = new MySqlCommand(query, connection);
            command.Parameters.AddWithValue("@EmpresaId", empresaId);

            using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                contratos.Add(new
                {
                    id = reader.GetInt32("id"),
                    chamado = reader.GetString("chamado"),
                    freelancer = reader.GetString("freelancer"),
                    valorFinal = reader.IsDBNull(reader.GetOrdinal("valor_final"))
                        ? 0
                        : reader.GetDecimal("valor_final"),
                    status = reader.GetString("status"),
                    dataInicio = reader.GetDateTime("data_inicio").ToString("yyyy-MM-dd HH:mm:ss"),
                    dataFinalizacao = reader.IsDBNull(reader.GetOrdinal("data_finalizacao"))
                        ? null
                        : reader.GetDateTime("data_finalizacao").ToString("yyyy-MM-dd HH:mm:ss")
                });
            }

            return Ok(contratos);
        }

        [HttpPost("{id}/finalizar")]
        [HttpPut("{id}/finalizar")]
        public async Task<IActionResult> FinalizarContrato(int id)
        {
            if (id <= 0)
            {
                return BadRequest(new
                {
                    mensagem = "Contrato inválido."
                });
            }

            var connectionString = _configuration.GetConnectionString("DefaultConnection");

            using var connection = new MySqlConnection(connectionString);
            await connection.OpenAsync();

            using var transaction = await connection.BeginTransactionAsync();

            try
            {
                var buscarQuery = @"
                    SELECT
                        id,
                        chamado_id,
                        status,
                        data_finalizacao
                    FROM contratos
                    WHERE id = @Id
                    LIMIT 1
                    FOR UPDATE;
                ";

                int chamadoId;
                string statusAtual;
                DateTime? dataFinalizacao = null;

                using (var buscarCommand = new MySqlCommand(buscarQuery, connection, transaction))
                {
                    buscarCommand.Parameters.AddWithValue("@Id", id);

                    using var reader = await buscarCommand.ExecuteReaderAsync();

                    if (!await reader.ReadAsync())
                    {
                        await transaction.RollbackAsync();

                        return NotFound(new
                        {
                            mensagem = "Contrato não encontrado."
                        });
                    }

                    chamadoId = reader.GetInt32("chamado_id");
                    statusAtual = reader.GetString("status");

                    if (!reader.IsDBNull(reader.GetOrdinal("data_finalizacao")))
                    {
                        dataFinalizacao = reader.GetDateTime("data_finalizacao");
                    }
                }

                var normalizedStatus = statusAtual.ToLower().Trim();

                if (normalizedStatus == "cancelado" || normalizedStatus == "cancelada")
                {
                    await transaction.RollbackAsync();

                    return BadRequest(new
                    {
                        mensagem = "Não é possível finalizar um contrato cancelado."
                    });
                }

                if (normalizedStatus == "finalizado")
                {
                    await transaction.CommitAsync();

                    return Ok(new
                    {
                        mensagem = "Contrato já estava finalizado.",
                        contratoId = id,
                        chamadoId,
                        status = "finalizado",
                        dataFinalizacao = dataFinalizacao?.ToString("yyyy-MM-dd HH:mm:ss")
                    });
                }

                var finalizarContratoQuery = @"
                    UPDATE contratos
                    SET
                        status = 'finalizado',
                        data_finalizacao = NOW()
                    WHERE id = @Id;
                ";

                using (var finalizarContratoCommand = new MySqlCommand(finalizarContratoQuery, connection, transaction))
                {
                    finalizarContratoCommand.Parameters.AddWithValue("@Id", id);
                    await finalizarContratoCommand.ExecuteNonQueryAsync();
                }

                var finalizarChamadoQuery = @"
                    UPDATE chamados
                    SET status = 'finalizado'
                    WHERE id = @ChamadoId;
                ";

                using (var finalizarChamadoCommand = new MySqlCommand(finalizarChamadoQuery, connection, transaction))
                {
                    finalizarChamadoCommand.Parameters.AddWithValue("@ChamadoId", chamadoId);
                    await finalizarChamadoCommand.ExecuteNonQueryAsync();
                }

                await transaction.CommitAsync();

                return Ok(new
                {
                    mensagem = "Contrato finalizado com sucesso.",
                    contratoId = id,
                    chamadoId,
                    status = "finalizado",
                    dataFinalizacao = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();

                return StatusCode(500, new
                {
                    mensagem = "Erro interno ao finalizar contrato.",
                    erro = ex.Message
                });
            }
        }
    }
}