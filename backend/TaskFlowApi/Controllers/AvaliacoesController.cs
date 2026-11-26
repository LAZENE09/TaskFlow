using Microsoft.AspNetCore.Mvc;
using MySqlConnector;

namespace TaskFlowApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AvaliacoesController : ControllerBase
    {
        private readonly IConfiguration _configuration;

        public AvaliacoesController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        [HttpPost]
        public async Task<IActionResult> CriarAvaliacao([FromBody] CriarAvaliacaoRequest request)
        {
            if (request.ContratoId <= 0)
                return BadRequest(new { mensagem = "Contrato inválido." });

            if (request.AvaliadorId <= 0)
                return BadRequest(new { mensagem = "Avaliador inválido." });

            if (request.Nota < 1 || request.Nota > 5)
                return BadRequest(new { mensagem = "A nota deve estar entre 1 e 5." });

            var comentarioNormalizado = string.IsNullOrWhiteSpace(request.Comentario)
                ? null
                : request.Comentario.Trim();

            if (!string.IsNullOrWhiteSpace(comentarioNormalizado) && comentarioNormalizado.Length > 500)
                return BadRequest(new { mensagem = "O comentário deve ter no máximo 500 caracteres." });

            try
            {
                var connectionString = _configuration.GetConnectionString("DefaultConnection");

                await using var connection = new MySqlConnection(connectionString);
                await connection.OpenAsync();

                var buscarContratoSql = @"
                    SELECT 
                        contratos.id,
                        contratos.empresa_id,
                        contratos.freelancer_id,
                        empresas.usuario_id AS empresa_usuario_id,
                        freelancers.usuario_id AS freelancer_usuario_id,
                        contratos.status
                    FROM contratos
                    INNER JOIN empresas ON empresas.id = contratos.empresa_id
                    INNER JOIN freelancers ON freelancers.id = contratos.freelancer_id
                    WHERE contratos.id = @contrato_id
                    LIMIT 1;
                ";

                int empresaPerfilId;
                int freelancerPerfilId;
                int empresaUsuarioId;
                int freelancerUsuarioId;
                string statusContrato;

                await using (var buscarContratoCommand = new MySqlCommand(buscarContratoSql, connection))
                {
                    buscarContratoCommand.Parameters.AddWithValue("@contrato_id", request.ContratoId);

                    await using var reader = await buscarContratoCommand.ExecuteReaderAsync();

                    if (!await reader.ReadAsync())
                    {
                        return BadRequest(new
                        {
                            mensagem = "Contrato não encontrado."
                        });
                    }

                    empresaPerfilId = reader.GetInt32("empresa_id");
                    freelancerPerfilId = reader.GetInt32("freelancer_id");
                    empresaUsuarioId = reader.GetInt32("empresa_usuario_id");
                    freelancerUsuarioId = reader.GetInt32("freelancer_usuario_id");
                    statusContrato = reader.GetString("status");
                }

                var statusNormalizado = statusContrato.ToLower().Trim();

                if (statusNormalizado != "finalizado" && statusNormalizado != "concluido" && statusNormalizado != "concluído")
                {
                    return BadRequest(new
                    {
                        mensagem = "Só é possível avaliar um serviço finalizado."
                    });
                }

                var avaliadorUsuarioId = request.AvaliadorId;

                if (avaliadorUsuarioId != empresaUsuarioId && avaliadorUsuarioId != freelancerUsuarioId)
                {
                    if (request.AvaliadorId == empresaPerfilId)
                    {
                        avaliadorUsuarioId = empresaUsuarioId;
                    }
                    else if (request.AvaliadorId == freelancerPerfilId)
                    {
                        avaliadorUsuarioId = freelancerUsuarioId;
                    }
                }

                var avaliadorEhEmpresa = avaliadorUsuarioId == empresaUsuarioId;
                var avaliadorEhFreelancer = avaliadorUsuarioId == freelancerUsuarioId;

                if (!avaliadorEhEmpresa && !avaliadorEhFreelancer)
                {
                    return BadRequest(new
                    {
                        mensagem = "O avaliador não faz parte deste contrato."
                    });
                }

                // A API usa o contrato como fonte da verdade.
                // Se o app mandar avaliadoId errado, por exemplo igual ao avaliadorId,
                // a API corrige para o outro participante do contrato.
                var avaliadoUsuarioId = avaliadorEhEmpresa
                    ? freelancerUsuarioId
                    : empresaUsuarioId;

                if (avaliadorUsuarioId == avaliadoUsuarioId)
                {
                    return BadRequest(new
                    {
                        mensagem = "O usuário não pode avaliar a si mesmo."
                    });
                }

                var verificarUsuariosSql = @"
                    SELECT COUNT(*)
                    FROM usuarios
                    WHERE id IN (@avaliador_id, @avaliado_id)
                    AND ativo = 1;
                ";

                await using (var verificarUsuariosCommand = new MySqlCommand(verificarUsuariosSql, connection))
                {
                    verificarUsuariosCommand.Parameters.AddWithValue("@avaliador_id", avaliadorUsuarioId);
                    verificarUsuariosCommand.Parameters.AddWithValue("@avaliado_id", avaliadoUsuarioId);

                    var usuariosValidos = Convert.ToInt32(await verificarUsuariosCommand.ExecuteScalarAsync());

                    if (usuariosValidos < 2)
                    {
                        return BadRequest(new
                        {
                            mensagem = "Avaliador ou usuário avaliado não encontrado."
                        });
                    }
                }

                var verificarAvaliacaoSql = @"
                    SELECT COUNT(*)
                    FROM avaliacoes
                    WHERE contrato_id = @contrato_id
                    AND avaliador_id = @avaliador_id;
                ";

                await using (var verificarAvaliacaoCommand = new MySqlCommand(verificarAvaliacaoSql, connection))
                {
                    verificarAvaliacaoCommand.Parameters.AddWithValue("@contrato_id", request.ContratoId);
                    verificarAvaliacaoCommand.Parameters.AddWithValue("@avaliador_id", avaliadorUsuarioId);

                    var jaAvaliou = Convert.ToInt32(await verificarAvaliacaoCommand.ExecuteScalarAsync());

                    if (jaAvaliou > 0)
                    {
                        return BadRequest(new
                        {
                            mensagem = "Você já avaliou este serviço."
                        });
                    }
                }

                var inserirSql = @"
                    INSERT INTO avaliacoes
                    (
                        contrato_id,
                        avaliador_id,
                        avaliado_id,
                        nota,
                        comentario,
                        data_avaliacao
                    )
                    VALUES
                    (
                        @contrato_id,
                        @avaliador_id,
                        @avaliado_id,
                        @nota,
                        @comentario,
                        NOW()
                    );

                    SELECT LAST_INSERT_ID();
                ";

                await using var command = new MySqlCommand(inserirSql, connection);

                command.Parameters.AddWithValue("@contrato_id", request.ContratoId);
                command.Parameters.AddWithValue("@avaliador_id", avaliadorUsuarioId);
                command.Parameters.AddWithValue("@avaliado_id", avaliadoUsuarioId);
                command.Parameters.AddWithValue("@nota", request.Nota);
                command.Parameters.AddWithValue(
                    "@comentario",
                    string.IsNullOrWhiteSpace(comentarioNormalizado)
                        ? DBNull.Value
                        : comentarioNormalizado
                );

                var idAvaliacao = Convert.ToInt32(await command.ExecuteScalarAsync());

                return Ok(new
                {
                    mensagem = "Avaliação salva com sucesso.",
                    id = idAvaliacao,
                    avaliacaoId = idAvaliacao,
                    avaliacao = new
                    {
                        id = idAvaliacao,
                        contratoId = request.ContratoId,
                        avaliadorId = avaliadorUsuarioId,
                        avaliadoId = avaliadoUsuarioId,
                        nota = request.Nota,
                        comentario = comentarioNormalizado
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    mensagem = "Erro interno ao salvar avaliação.",
                    erro = ex.Message
                });
            }
        }

        [HttpGet("usuario/{idUsuario}")]
        public async Task<IActionResult> BuscarAvaliacoesDoUsuario(int idUsuario)
        {
            if (idUsuario <= 0)
                return BadRequest(new { mensagem = "Usuário inválido." });

            try
            {
                var connectionString = _configuration.GetConnectionString("DefaultConnection");

                await using var connection = new MySqlConnection(connectionString);
                await connection.OpenAsync();

                var sql = @"
                    SELECT
                        a.id,
                        a.contrato_id,
                        a.avaliador_id,
                        u.nome AS nome_avaliador,
                        a.avaliado_id,
                        a.nota,
                        a.comentario,
                        a.data_avaliacao
                    FROM avaliacoes a
                    INNER JOIN usuarios u ON u.id = a.avaliador_id
                    WHERE a.avaliado_id = @id_usuario
                    ORDER BY a.data_avaliacao DESC;
                ";

                await using var command = new MySqlCommand(sql, connection);
                command.Parameters.AddWithValue("@id_usuario", idUsuario);

                await using var reader = await command.ExecuteReaderAsync();

                var avaliacoes = new List<object>();
                var somaNotas = 0;
                var total = 0;

                while (await reader.ReadAsync())
                {
                    var nota = reader.GetInt32("nota");

                    somaNotas += nota;
                    total++;

                    avaliacoes.Add(new
                    {
                        id = reader.GetInt32("id"),
                        contratoId = reader.GetInt32("contrato_id"),
                        avaliadorId = reader.GetInt32("avaliador_id"),
                        nomeAvaliador = reader.GetString("nome_avaliador"),
                        avaliadoId = reader.GetInt32("avaliado_id"),
                        nota,
                        comentario = reader.IsDBNull(reader.GetOrdinal("comentario"))
                            ? null
                            : reader.GetString("comentario"),
                        dataAvaliacao = reader.GetDateTime("data_avaliacao")
                    });
                }

                var media = total > 0
                    ? Math.Round((decimal)somaNotas / total, 1)
                    : 0;

                return Ok(new
                {
                    media,
                    total,
                    avaliacoes
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    mensagem = "Erro interno ao buscar avaliações.",
                    erro = ex.Message
                });
            }
        }


        [HttpGet("freelancer/{idFreelancer}")]
        public async Task<IActionResult> BuscarAvaliacoesDoFreelancer(int idFreelancer)
        {
            if (idFreelancer <= 0)
                return BadRequest(new { mensagem = "Freelancer inválido." });

            try
            {
                var connectionString = _configuration.GetConnectionString("DefaultConnection");

                await using var connection = new MySqlConnection(connectionString);
                await connection.OpenAsync();

                var sql = @"
                    SELECT usuario_id
                    FROM freelancers
                    WHERE id = @id_freelancer
                    LIMIT 1;
                ";

                await using var command = new MySqlCommand(sql, connection);
                command.Parameters.AddWithValue("@id_freelancer", idFreelancer);

                var result = await command.ExecuteScalarAsync();

                if (result == null || result == DBNull.Value)
                {
                    // Fallback: em algumas telas antigas o app manda usuario_id no campo freelancerId.
                    return await BuscarAvaliacoesDoUsuario(idFreelancer);
                }

                var usuarioId = Convert.ToInt32(result);

                return await BuscarAvaliacoesDoUsuario(usuarioId);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    mensagem = "Erro interno ao buscar avaliações do freelancer.",
                    erro = ex.Message
                });
            }
        }

        [HttpGet("contrato/{contratoId}")]
        public async Task<IActionResult> BuscarAvaliacoesDoContrato(int contratoId)
        {
            if (contratoId <= 0)
                return BadRequest(new { mensagem = "Contrato inválido." });

            try
            {
                var connectionString = _configuration.GetConnectionString("DefaultConnection");

                await using var connection = new MySqlConnection(connectionString);
                await connection.OpenAsync();

                var sql = @"
                    SELECT
                        a.id,
                        a.contrato_id,
                        a.avaliador_id,
                        avaliador.nome AS nome_avaliador,
                        a.avaliado_id,
                        avaliado.nome AS nome_avaliado,
                        a.nota,
                        a.comentario,
                        a.data_avaliacao
                    FROM avaliacoes a
                    INNER JOIN usuarios avaliador ON avaliador.id = a.avaliador_id
                    INNER JOIN usuarios avaliado ON avaliado.id = a.avaliado_id
                    WHERE a.contrato_id = @contrato_id
                    ORDER BY a.data_avaliacao DESC;
                ";

                await using var command = new MySqlCommand(sql, connection);
                command.Parameters.AddWithValue("@contrato_id", contratoId);

                await using var reader = await command.ExecuteReaderAsync();

                var avaliacoes = new List<object>();

                while (await reader.ReadAsync())
                {
                    avaliacoes.Add(new
                    {
                        id = reader.GetInt32("id"),
                        contratoId = reader.GetInt32("contrato_id"),
                        avaliadorId = reader.GetInt32("avaliador_id"),
                        nomeAvaliador = reader.GetString("nome_avaliador"),
                        avaliadoId = reader.GetInt32("avaliado_id"),
                        nomeAvaliado = reader.GetString("nome_avaliado"),
                        nota = reader.GetInt32("nota"),
                        comentario = reader.IsDBNull(reader.GetOrdinal("comentario"))
                            ? null
                            : reader.GetString("comentario"),
                        dataAvaliacao = reader.GetDateTime("data_avaliacao")
                    });
                }

                return Ok(new
                {
                    total = avaliacoes.Count,
                    avaliacoes
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    mensagem = "Erro interno ao buscar avaliações do contrato.",
                    erro = ex.Message
                });
            }
        }
    }

    public class CriarAvaliacaoRequest
    {
        public int ContratoId { get; set; }
        public int AvaliadorId { get; set; }
        public int AvaliadoId { get; set; }
        public int Nota { get; set; }
        public string? Comentario { get; set; }
    }
}
