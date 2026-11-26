using Microsoft.AspNetCore.Mvc;
using MySqlConnector;

namespace TaskFlowApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CandidaturasController : ControllerBase
    {
        private readonly IConfiguration _configuration;

        public CandidaturasController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public class CriarCandidaturaRequest
        {
            public int ChamadoId { get; set; }
            public int FreelancerId { get; set; }
            public string? Mensagem { get; set; }
            public decimal? ValorProposto { get; set; }
        }

        private static bool IsAcceptedStatus(string? status)
        {
            var normalizedStatus = status?.ToLower().Trim();

            return normalizedStatus == "aceita" ||
                   normalizedStatus == "aceito" ||
                   normalizedStatus == "accepted" ||
                   normalizedStatus == "aprovada" ||
                   normalizedStatus == "aprovado" ||
                   normalizedStatus == "contratada" ||
                   normalizedStatus == "contratado";
        }

        private static bool IsUnavailableChamadoStatus(string? status)
        {
            var normalizedStatus = status?.ToLower().Trim();

            return normalizedStatus == "finalizado" ||
                   normalizedStatus == "concluido" ||
                   normalizedStatus == "concluído" ||
                   normalizedStatus == "finished" ||
                   normalizedStatus == "cancelado" ||
                   normalizedStatus == "cancelada" ||
                   normalizedStatus == "canceled" ||
                   normalizedStatus == "cancelled" ||
                   normalizedStatus == "em_andamento";
        }

        [HttpGet]
        public async Task<IActionResult> ListarCandidaturas()
        {
            var candidaturas = new List<object>();

            var connectionString = _configuration.GetConnectionString("DefaultConnection");

            using var connection = new MySqlConnection(connectionString);
            await connection.OpenAsync();

            var query = @"
                SELECT 
                    candidaturas.id,
                    candidaturas.chamado_id,
                    candidaturas.freelancer_id,
                    chamados.titulo AS chamado,
                    usuarios.id AS freelancer_usuario_id,
                    usuarios.nome AS freelancer,
                    usuarios.email,
                    candidaturas.mensagem,
                    candidaturas.valor_proposto,
                    candidaturas.status,
                    candidaturas.data_candidatura,
                    contratos.id AS contrato_id
                FROM candidaturas
                INNER JOIN chamados ON candidaturas.chamado_id = chamados.id
                INNER JOIN freelancers ON candidaturas.freelancer_id = freelancers.id
                INNER JOIN usuarios ON freelancers.usuario_id = usuarios.id
                LEFT JOIN contratos ON contratos.candidatura_id = candidaturas.id
                ORDER BY candidaturas.id DESC;
            ";

            using var command = new MySqlCommand(query, connection);
            using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                candidaturas.Add(new
                {
                    id = reader.GetInt32("id"),
                    chamadoId = reader.GetInt32("chamado_id"),
                    freelancerId = reader.GetInt32("freelancer_id"),
                    freelancerUsuarioId = reader.IsDBNull(reader.GetOrdinal("freelancer_usuario_id"))
                        ? (int?)null
                        : reader.GetInt32("freelancer_usuario_id"),
                    contratoId = reader.IsDBNull(reader.GetOrdinal("contrato_id"))
                        ? (int?)null
                        : reader.GetInt32("contrato_id"),
                    chamado = reader.GetString("chamado"),
                    freelancer = reader.GetString("freelancer"),
                    email = reader.GetString("email"),
                    mensagem = reader.IsDBNull(reader.GetOrdinal("mensagem"))
                        ? null
                        : reader.GetString("mensagem"),
                    valorProposto = reader.IsDBNull(reader.GetOrdinal("valor_proposto"))
                        ? 0
                        : reader.GetDecimal("valor_proposto"),
                    status = reader.GetString("status"),
                    dataCandidatura = reader.GetDateTime("data_candidatura").ToString("yyyy-MM-dd HH:mm:ss")
                });
            }

            return Ok(candidaturas);
        }

        [HttpGet("freelancer/{freelancerId}")]
        public async Task<IActionResult> ListarPorFreelancer(int freelancerId)
        {
            var candidaturas = new List<object>();

            var connectionString = _configuration.GetConnectionString("DefaultConnection");

            using var connection = new MySqlConnection(connectionString);
            await connection.OpenAsync();

            var query = @"
                SELECT 
                    candidaturas.id,
                    candidaturas.chamado_id,
                    candidaturas.freelancer_id,

                    chamados.titulo AS chamado,
                    chamados.cidade,
                    chamados.estado,
                    chamados.local_servico,
                    chamados.valor_estimado,
                    chamados.data_servico,
                    chamados.hora_servico,
                    chamados.status AS chamado_status,

                    empresas.id AS empresa_id,
                    empresas.usuario_id AS empresa_usuario_id,
                    usuarios_empresa.nome AS empresa,

                    candidaturas.mensagem,
                    candidaturas.valor_proposto,
                    candidaturas.status,
                    candidaturas.data_candidatura,

                    contratos.id AS contrato_id
                FROM candidaturas
                INNER JOIN chamados ON candidaturas.chamado_id = chamados.id
                INNER JOIN empresas ON chamados.empresa_id = empresas.id
                INNER JOIN usuarios AS usuarios_empresa ON empresas.usuario_id = usuarios_empresa.id
                LEFT JOIN contratos ON contratos.candidatura_id = candidaturas.id
                WHERE candidaturas.freelancer_id = @FreelancerId
                ORDER BY candidaturas.id DESC;
            ";

            using var command = new MySqlCommand(query, connection);
            command.Parameters.AddWithValue("@FreelancerId", freelancerId);

            using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                candidaturas.Add(new
                {
                    id = reader.GetInt32("id"),
                    chamadoId = reader.GetInt32("chamado_id"),
                    freelancerId = reader.GetInt32("freelancer_id"),

                    contratoId = reader.IsDBNull(reader.GetOrdinal("contrato_id"))
                        ? (int?)null
                        : reader.GetInt32("contrato_id"),

                    empresaId = reader.IsDBNull(reader.GetOrdinal("empresa_id"))
                        ? (int?)null
                        : reader.GetInt32("empresa_id"),

                    empresaUsuarioId = reader.IsDBNull(reader.GetOrdinal("empresa_usuario_id"))
                        ? (int?)null
                        : reader.GetInt32("empresa_usuario_id"),

                    empresa = reader.IsDBNull(reader.GetOrdinal("empresa"))
                        ? "Empresa"
                        : reader.GetString("empresa"),

                    chamado = reader.GetString("chamado"),

                    cidade = reader.IsDBNull(reader.GetOrdinal("cidade"))
                        ? null
                        : reader.GetString("cidade"),

                    estado = reader.IsDBNull(reader.GetOrdinal("estado"))
                        ? null
                        : reader.GetString("estado"),

                    localServico = reader.IsDBNull(reader.GetOrdinal("local_servico"))
                        ? null
                        : reader.GetString("local_servico"),

                    valorEstimado = reader.IsDBNull(reader.GetOrdinal("valor_estimado"))
                        ? 0
                        : reader.GetDecimal("valor_estimado"),

                    dataServico = reader.IsDBNull(reader.GetOrdinal("data_servico"))
                        ? null
                        : reader.GetDateTime("data_servico").ToString("yyyy-MM-dd"),

                    horaServico = reader.IsDBNull(reader.GetOrdinal("hora_servico"))
                        ? null
                        : reader.GetTimeSpan("hora_servico").ToString(@"hh\:mm"),

                    chamadoStatus = reader.GetString("chamado_status"),

                    mensagem = reader.IsDBNull(reader.GetOrdinal("mensagem"))
                        ? null
                        : reader.GetString("mensagem"),

                    valorProposto = reader.IsDBNull(reader.GetOrdinal("valor_proposto"))
                        ? 0
                        : reader.GetDecimal("valor_proposto"),

                    status = reader.GetString("status"),

                    dataCandidatura = reader.GetDateTime("data_candidatura").ToString("yyyy-MM-dd HH:mm:ss")
                });
            }

            return Ok(candidaturas);
        }

        [HttpGet("chamado/{chamadoId}")]
        public async Task<IActionResult> ListarPorChamado(int chamadoId)
        {
            var candidaturas = new List<object>();

            var connectionString = _configuration.GetConnectionString("DefaultConnection");

            using var connection = new MySqlConnection(connectionString);
            await connection.OpenAsync();

            var query = @"
                SELECT 
                    candidaturas.id,
                    candidaturas.chamado_id,
                    candidaturas.freelancer_id,
                    usuarios.id AS freelancer_usuario_id,
                    usuarios.nome AS freelancer,
                    usuarios.email,
                    candidaturas.mensagem,
                    candidaturas.valor_proposto,
                    candidaturas.status,
                    candidaturas.data_candidatura,
                    contratos.id AS contrato_id
                FROM candidaturas
                INNER JOIN freelancers ON candidaturas.freelancer_id = freelancers.id
                INNER JOIN usuarios ON freelancers.usuario_id = usuarios.id
                LEFT JOIN contratos ON contratos.candidatura_id = candidaturas.id
                WHERE candidaturas.chamado_id = @ChamadoId
                ORDER BY candidaturas.id DESC;
            ";

            using var command = new MySqlCommand(query, connection);
            command.Parameters.AddWithValue("@ChamadoId", chamadoId);

            using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                candidaturas.Add(new
                {
                    id = reader.GetInt32("id"),
                    chamadoId = reader.GetInt32("chamado_id"),
                    freelancerId = reader.GetInt32("freelancer_id"),
                    freelancerUsuarioId = reader.IsDBNull(reader.GetOrdinal("freelancer_usuario_id"))
                        ? (int?)null
                        : reader.GetInt32("freelancer_usuario_id"),
                    contratoId = reader.IsDBNull(reader.GetOrdinal("contrato_id"))
                        ? (int?)null
                        : reader.GetInt32("contrato_id"),
                    freelancer = reader.GetString("freelancer"),
                    email = reader.GetString("email"),
                    mensagem = reader.IsDBNull(reader.GetOrdinal("mensagem"))
                        ? null
                        : reader.GetString("mensagem"),
                    valorProposto = reader.IsDBNull(reader.GetOrdinal("valor_proposto"))
                        ? 0
                        : reader.GetDecimal("valor_proposto"),
                    status = reader.GetString("status"),
                    dataCandidatura = reader.GetDateTime("data_candidatura").ToString("yyyy-MM-dd HH:mm:ss")
                });
            }

            return Ok(candidaturas);
        }

        [HttpPost]
        public async Task<IActionResult> CriarCandidatura([FromBody] CriarCandidaturaRequest request)
        {
            if (request.ChamadoId <= 0)
                return BadRequest(new { mensagem = "Chamado é obrigatório." });

            if (request.FreelancerId <= 0)
                return BadRequest(new { mensagem = "Freelancer é obrigatório." });

            var connectionString = _configuration.GetConnectionString("DefaultConnection");

            using var connection = new MySqlConnection(connectionString);
            await connection.OpenAsync();

            var verificarChamadoQuery = @"
                SELECT
                    chamados.id,
                    chamados.status,
                    COALESCE(chamados.quantidade_profissionais, 1) AS quantidade_profissionais,
                    (
                        SELECT COUNT(*)
                        FROM candidaturas c
                        WHERE c.chamado_id = chamados.id
                        AND LOWER(c.status) IN (
                            'aceita',
                            'aceito',
                            'accepted',
                            'aprovada',
                            'aprovado',
                            'contratada',
                            'contratado'
                        )
                    ) AS total_aceitos
                FROM chamados
                WHERE chamados.id = @ChamadoId;
            ";

            using (var verificarChamadoCommand = new MySqlCommand(verificarChamadoQuery, connection))
            {
                verificarChamadoCommand.Parameters.AddWithValue("@ChamadoId", request.ChamadoId);

                using var reader = await verificarChamadoCommand.ExecuteReaderAsync();

                if (!await reader.ReadAsync())
                {
                    return NotFound(new
                    {
                        mensagem = "Chamado não encontrado."
                    });
                }

                var chamadoStatus = reader.GetString("status");
                var quantidadeProfissionais = Convert.ToInt32(reader["quantidade_profissionais"]);
                var totalAceitos = Convert.ToInt32(reader["total_aceitos"]);
                var vagasDisponiveis = Math.Max(quantidadeProfissionais - totalAceitos, 0);

                if (IsUnavailableChamadoStatus(chamadoStatus) || vagasDisponiveis <= 0)
                {
                    return BadRequest(new
                    {
                        mensagem = "Este chamado não possui vagas disponíveis."
                    });
                }
            }

            var verificarCandidaturaQuery = @"
                SELECT COUNT(*) 
                FROM candidaturas
                WHERE chamado_id = @ChamadoId
                AND freelancer_id = @FreelancerId;
            ";

            using (var verificarCandidaturaCommand = new MySqlCommand(verificarCandidaturaQuery, connection))
            {
                verificarCandidaturaCommand.Parameters.AddWithValue("@ChamadoId", request.ChamadoId);
                verificarCandidaturaCommand.Parameters.AddWithValue("@FreelancerId", request.FreelancerId);

                var jaExiste = Convert.ToInt32(await verificarCandidaturaCommand.ExecuteScalarAsync());

                if (jaExiste > 0)
                {
                    return Conflict(new
                    {
                        mensagem = "Este freelancer já se candidatou a este chamado."
                    });
                }
            }

            var query = @"
                INSERT INTO candidaturas
                (
                    chamado_id,
                    freelancer_id,
                    mensagem,
                    valor_proposto,
                    status
                )
                VALUES
                (
                    @ChamadoId,
                    @FreelancerId,
                    @Mensagem,
                    @ValorProposto,
                    'pendente'
                );

                SELECT LAST_INSERT_ID();
            ";

            using var command = new MySqlCommand(query, connection);

            command.Parameters.AddWithValue("@ChamadoId", request.ChamadoId);
            command.Parameters.AddWithValue("@FreelancerId", request.FreelancerId);
            command.Parameters.AddWithValue(
                "@Mensagem",
                string.IsNullOrWhiteSpace(request.Mensagem)
                    ? DBNull.Value
                    : request.Mensagem.Trim()
            );
            command.Parameters.AddWithValue(
                "@ValorProposto",
                request.ValorProposto.HasValue
                    ? request.ValorProposto.Value
                    : DBNull.Value
            );

            var candidaturaId = Convert.ToInt32(await command.ExecuteScalarAsync());

            return Created("", new
            {
                mensagem = "Candidatura enviada com sucesso.",
                candidatura = new
                {
                    id = candidaturaId,
                    chamadoId = request.ChamadoId,
                    freelancerId = request.FreelancerId,
                    mensagem = request.Mensagem,
                    valorProposto = request.ValorProposto,
                    status = "pendente"
                }
            });
        }

        [HttpPost("{id}/aceitar")]
        public async Task<IActionResult> AceitarCandidatura(int id)
        {
            if (id <= 0)
                return BadRequest(new { mensagem = "Candidatura inválida." });

            var connectionString = _configuration.GetConnectionString("DefaultConnection");

            using var connection = new MySqlConnection(connectionString);
            await connection.OpenAsync();

            using var transaction = await connection.BeginTransactionAsync();

            try
            {
                var buscarQuery = @"
                    SELECT
                        candidaturas.id,
                        candidaturas.chamado_id,
                        candidaturas.freelancer_id,
                        candidaturas.status,
                        candidaturas.valor_proposto,
                        chamados.empresa_id,
                        chamados.valor_estimado,
                        chamados.status AS chamado_status,
                        COALESCE(chamados.quantidade_profissionais, 1) AS quantidade_profissionais,
                        (
                            SELECT COUNT(*)
                            FROM candidaturas c
                            WHERE c.chamado_id = candidaturas.chamado_id
                            AND LOWER(c.status) IN (
                                'aceita',
                                'aceito',
                                'accepted',
                                'aprovada',
                                'aprovado',
                                'contratada',
                                'contratado'
                            )
                        ) AS total_aceitos
                    FROM candidaturas
                    INNER JOIN chamados ON candidaturas.chamado_id = chamados.id
                    WHERE candidaturas.id = @Id
                    FOR UPDATE;
                ";

                int chamadoId;
                int freelancerId;
                int empresaId;
                int quantidadeProfissionais;
                int totalAceitos;
                decimal valorFinal;
                string statusAtual;
                string chamadoStatus;

                using (var buscarCommand = new MySqlCommand(buscarQuery, connection, transaction))
                {
                    buscarCommand.Parameters.AddWithValue("@Id", id);

                    using var reader = await buscarCommand.ExecuteReaderAsync();

                    if (!await reader.ReadAsync())
                    {
                        await transaction.RollbackAsync();

                        return NotFound(new
                        {
                            mensagem = "Candidatura não encontrada."
                        });
                    }

                    chamadoId = reader.GetInt32("chamado_id");
                    freelancerId = reader.GetInt32("freelancer_id");
                    empresaId = reader.GetInt32("empresa_id");
                    statusAtual = reader.GetString("status");
                    chamadoStatus = reader.GetString("chamado_status");

                    var valorProposto = reader.IsDBNull(reader.GetOrdinal("valor_proposto"))
                        ? (decimal?)null
                        : reader.GetDecimal("valor_proposto");

                    var valorEstimado = reader.IsDBNull(reader.GetOrdinal("valor_estimado"))
                        ? (decimal?)null
                        : reader.GetDecimal("valor_estimado");

                    valorFinal = valorProposto ?? valorEstimado ?? 0;
                    quantidadeProfissionais = Convert.ToInt32(reader["quantidade_profissionais"]);
                    totalAceitos = Convert.ToInt32(reader["total_aceitos"]);
                }

                async Task<int> ObterOuCriarContratoAsync()
                {
                    var contratoQuery = @"
                        INSERT INTO contratos
                        (
                            chamado_id,
                            empresa_id,
                            freelancer_id,
                            candidatura_id,
                            valor_final,
                            status,
                            data_inicio,
                            data_finalizacao
                        )
                        SELECT
                            @ChamadoId,
                            @EmpresaId,
                            @FreelancerId,
                            @CandidaturaId,
                            @ValorFinal,
                            'ativo',
                            NOW(),
                            NULL
                        WHERE NOT EXISTS (
                            SELECT 1
                            FROM contratos
                            WHERE candidatura_id = @CandidaturaId
                        );

                        SELECT id
                        FROM contratos
                        WHERE candidatura_id = @CandidaturaId
                        LIMIT 1;
                    ";

                    using var contratoCommand = new MySqlCommand(contratoQuery, connection, transaction);
                    contratoCommand.Parameters.AddWithValue("@ChamadoId", chamadoId);
                    contratoCommand.Parameters.AddWithValue("@EmpresaId", empresaId);
                    contratoCommand.Parameters.AddWithValue("@FreelancerId", freelancerId);
                    contratoCommand.Parameters.AddWithValue("@CandidaturaId", id);
                    contratoCommand.Parameters.AddWithValue("@ValorFinal", valorFinal);

                    var contratoResult = await contratoCommand.ExecuteScalarAsync();

                    return Convert.ToInt32(contratoResult);
                }

                if (IsAcceptedStatus(statusAtual))
                {
                    var contratoIdAtual = await ObterOuCriarContratoAsync();
                    var vagasRestantes = Math.Max(quantidadeProfissionais - totalAceitos, 0);

                    await transaction.CommitAsync();

                    return Ok(new
                    {
                        mensagem = "Candidatura já estava aceita.",
                        candidaturaId = id,
                        contratoId = contratoIdAtual,
                        chamadoId,
                        freelancerId,
                        quantidadeProfissionais,
                        totalAceitos,
                        vagasDisponiveis = vagasRestantes,
                        chamadoStatus
                    });
                }

                if (IsUnavailableChamadoStatus(chamadoStatus))
                {
                    await transaction.RollbackAsync();

                    return BadRequest(new
                    {
                        mensagem = "Este chamado não permite novos aceites."
                    });
                }

                if (statusAtual.ToLower().Trim() == "recusada" ||
                    statusAtual.ToLower().Trim() == "recusado")
                {
                    await transaction.RollbackAsync();

                    return BadRequest(new
                    {
                        mensagem = "Não é possível aceitar uma candidatura recusada."
                    });
                }

                if (totalAceitos >= quantidadeProfissionais)
                {
                    await transaction.RollbackAsync();

                    return BadRequest(new
                    {
                        mensagem = "Todas as vagas deste chamado já foram preenchidas."
                    });
                }

                var aceitarQuery = @"
                    UPDATE candidaturas
                    SET status = 'aceita'
                    WHERE id = @Id;
                ";

                using (var aceitarCommand = new MySqlCommand(aceitarQuery, connection, transaction))
                {
                    aceitarCommand.Parameters.AddWithValue("@Id", id);
                    await aceitarCommand.ExecuteNonQueryAsync();
                }

                var totalAceitosDepois = totalAceitos + 1;
                var vagasDisponiveisDepois = Math.Max(quantidadeProfissionais - totalAceitosDepois, 0);

                var novoStatusChamado =
                    vagasDisponiveisDepois <= 0
                        ? "em_andamento"
                        : "aberto";

                var atualizarChamadoQuery = @"
                    UPDATE chamados
                    SET status = @Status
                    WHERE id = @ChamadoId;
                ";

                using (var atualizarChamadoCommand = new MySqlCommand(atualizarChamadoQuery, connection, transaction))
                {
                    atualizarChamadoCommand.Parameters.AddWithValue("@Status", novoStatusChamado);
                    atualizarChamadoCommand.Parameters.AddWithValue("@ChamadoId", chamadoId);
                    await atualizarChamadoCommand.ExecuteNonQueryAsync();
                }

                var contratoIdCriado = await ObterOuCriarContratoAsync();

                await transaction.CommitAsync();

                return Ok(new
                {
                    mensagem = "Candidatura aceita com sucesso.",
                    candidaturaId = id,
                    contratoId = contratoIdCriado,
                    chamadoId,
                    freelancerId,
                    quantidadeProfissionais,
                    totalAceitos = totalAceitosDepois,
                    vagasDisponiveis = vagasDisponiveisDepois,
                    chamadoStatus = novoStatusChamado
                });
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        [HttpPost("{id}/recusar")]
        public async Task<IActionResult> RecusarCandidatura(int id)
        {
            if (id <= 0)
                return BadRequest(new { mensagem = "Candidatura inválida." });

            var connectionString = _configuration.GetConnectionString("DefaultConnection");

            using var connection = new MySqlConnection(connectionString);
            await connection.OpenAsync();

            var verificarQuery = @"
                SELECT status
                FROM candidaturas
                WHERE id = @Id;
            ";

            string? statusAtual;

            using (var verificarCommand = new MySqlCommand(verificarQuery, connection))
            {
                verificarCommand.Parameters.AddWithValue("@Id", id);

                var result = await verificarCommand.ExecuteScalarAsync();

                if (result == null)
                {
                    return NotFound(new
                    {
                        mensagem = "Candidatura não encontrada."
                    });
                }

                statusAtual = Convert.ToString(result);
            }

            if (IsAcceptedStatus(statusAtual))
            {
                return BadRequest(new
                {
                    mensagem = "Não é possível recusar uma candidatura já aceita."
                });
            }

            var recusarQuery = @"
                UPDATE candidaturas
                SET status = 'recusada'
                WHERE id = @Id;
            ";

            using var recusarCommand = new MySqlCommand(recusarQuery, connection);
            recusarCommand.Parameters.AddWithValue("@Id", id);

            await recusarCommand.ExecuteNonQueryAsync();

            return Ok(new
            {
                mensagem = "Candidatura recusada com sucesso.",
                candidaturaId = id,
                status = "recusada"
            });
        }
    }
}
