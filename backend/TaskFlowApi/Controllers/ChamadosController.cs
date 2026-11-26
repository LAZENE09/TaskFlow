using Microsoft.AspNetCore.Mvc;
using MySqlConnector;
using System.Net;
using System.Text.Json;

namespace TaskFlowApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ChamadosController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private static readonly HttpClient TomTomHttpClient = new HttpClient();

        public ChamadosController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public class CriarChamadoRequest
        {
            public int EmpresaId { get; set; }
            public int CategoriaId { get; set; }
            public string Titulo { get; set; } = string.Empty;
            public string Descricao { get; set; } = string.Empty;
            public string? LocalServico { get; set; }
            public string? Cidade { get; set; }
            public string? Estado { get; set; }
            public DateTime? DataServico { get; set; }
            public string? HoraServico { get; set; }
            public decimal? ValorEstimado { get; set; }
            public int QuantidadeProfissionais { get; set; } = 1;
            public decimal? Latitude { get; set; }
            public decimal? Longitude { get; set; }
        }

        private async Task<(decimal? Latitude, decimal? Longitude)> BuscarCoordenadasTomTomAsync(
            CriarChamadoRequest request
        )
        {
            if (request.Latitude.HasValue && request.Longitude.HasValue)
            {
                return (request.Latitude.Value, request.Longitude.Value);
            }

            var tomTomApiKey = _configuration["TomTom:ApiKey"];

            if (string.IsNullOrWhiteSpace(tomTomApiKey))
            {
                Console.WriteLine("Chave da TomTom não configurada no appsettings.json.");
                return (null, null);
            }

            var partesEndereco = new List<string>();

            if (!string.IsNullOrWhiteSpace(request.LocalServico))
                partesEndereco.Add(request.LocalServico.Trim());

            if (!string.IsNullOrWhiteSpace(request.Cidade))
                partesEndereco.Add(request.Cidade.Trim());

            if (!string.IsNullOrWhiteSpace(request.Estado))
                partesEndereco.Add(request.Estado.Trim());

            partesEndereco.Add("Brasil");

            var enderecoCompleto = string.Join(", ", partesEndereco);

            if (string.IsNullOrWhiteSpace(enderecoCompleto))
            {
                return (null, null);
            }

            try
            {
                var enderecoUrl = WebUtility.UrlEncode(enderecoCompleto);

                var url =
                    $"https://api.tomtom.com/search/2/geocode/{enderecoUrl}.json" +
                    $"?key={tomTomApiKey}&limit=1&countrySet=BR&language=pt-BR";

                using var response = await TomTomHttpClient.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"Erro TomTom: {response.StatusCode}");
                    return (null, null);
                }

                await using var stream = await response.Content.ReadAsStreamAsync();
                using var json = await JsonDocument.ParseAsync(stream);

                if (!json.RootElement.TryGetProperty("results", out var results) ||
                    results.GetArrayLength() == 0)
                {
                    Console.WriteLine($"TomTom não encontrou coordenadas para: {enderecoCompleto}");
                    return (null, null);
                }

                var primeiroResultado = results[0];

                if (!primeiroResultado.TryGetProperty("position", out var position))
                {
                    return (null, null);
                }

                var latitude = (decimal)position.GetProperty("lat").GetDouble();
                var longitude = (decimal)position.GetProperty("lon").GetDouble();

                return (latitude, longitude);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao buscar coordenadas na TomTom: {ex.Message}");
                return (null, null);
            }
        }

        [HttpGet]
        public async Task<IActionResult> ListarChamados()
        {
            var chamados = new List<object>();

            var connectionString = _configuration.GetConnectionString("DefaultConnection");

            using var connection = new MySqlConnection(connectionString);
            await connection.OpenAsync();

            var query = @"
                SELECT 
                    chamados.id,
                    chamados.empresa_id,
                    chamados.categoria_id,
                    chamados.titulo,
                    chamados.descricao,
                    categorias.nome AS categoria,
                    empresas.nome_fantasia AS empresa,
                    chamados.local_servico,
                    chamados.cidade,
                    chamados.estado,
                    chamados.data_servico,
                    chamados.hora_servico,
                    chamados.valor_estimado,
                    chamados.latitude,
                    chamados.longitude,
                    COALESCE(chamados.quantidade_profissionais, 1) AS quantidade_profissionais,
                    chamados.status,

                    (
                        SELECT COUNT(*)
                        FROM candidaturas c
                        WHERE c.chamado_id = chamados.id
                    ) AS total_candidaturas,

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
                    ) AS total_aceitos,

                    GREATEST(
                        COALESCE(chamados.quantidade_profissionais, 1) - (
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
                        ),
                        0
                    ) AS vagas_disponiveis

                FROM chamados
                INNER JOIN categorias ON chamados.categoria_id = categorias.id
                INNER JOIN empresas ON chamados.empresa_id = empresas.id
                WHERE chamados.status IN ('aberto', 'em_andamento', 'finalizado')
                ORDER BY chamados.id DESC;
            ";

            using var command = new MySqlCommand(query, connection);
            using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                var idOrdinal = reader.GetOrdinal("id");
                var empresaIdOrdinal = reader.GetOrdinal("empresa_id");
                var categoriaIdOrdinal = reader.GetOrdinal("categoria_id");
                var tituloOrdinal = reader.GetOrdinal("titulo");
                var descricaoOrdinal = reader.GetOrdinal("descricao");
                var categoriaOrdinal = reader.GetOrdinal("categoria");
                var empresaOrdinal = reader.GetOrdinal("empresa");
                var localServicoOrdinal = reader.GetOrdinal("local_servico");
                var cidadeOrdinal = reader.GetOrdinal("cidade");
                var estadoOrdinal = reader.GetOrdinal("estado");
                var dataServicoOrdinal = reader.GetOrdinal("data_servico");
                var horaServicoOrdinal = reader.GetOrdinal("hora_servico");
                var valorEstimadoOrdinal = reader.GetOrdinal("valor_estimado");
                var latitudeOrdinal = reader.GetOrdinal("latitude");
                var longitudeOrdinal = reader.GetOrdinal("longitude");
                var quantidadeProfissionaisOrdinal = reader.GetOrdinal("quantidade_profissionais");
                var statusOrdinal = reader.GetOrdinal("status");
                var totalCandidaturasOrdinal = reader.GetOrdinal("total_candidaturas");
                var totalAceitosOrdinal = reader.GetOrdinal("total_aceitos");
                var vagasDisponiveisOrdinal = reader.GetOrdinal("vagas_disponiveis");

                chamados.Add(new
                {
                    id = reader.GetInt32(idOrdinal),
                    empresaId = reader.GetInt32(empresaIdOrdinal),
                    categoriaId = reader.GetInt32(categoriaIdOrdinal),
                    titulo = reader.GetString(tituloOrdinal),
                    descricao = reader.GetString(descricaoOrdinal),
                    categoria = reader.GetString(categoriaOrdinal),
                    empresa = reader.GetString(empresaOrdinal),

                    localServico = reader.IsDBNull(localServicoOrdinal)
                        ? null
                        : reader.GetString(localServicoOrdinal),

                    cidade = reader.IsDBNull(cidadeOrdinal)
                        ? null
                        : reader.GetString(cidadeOrdinal),

                    estado = reader.IsDBNull(estadoOrdinal)
                        ? null
                        : reader.GetString(estadoOrdinal),

                    dataServico = reader.IsDBNull(dataServicoOrdinal)
                        ? null
                        : reader.GetDateTime(dataServicoOrdinal).ToString("yyyy-MM-dd"),

                    horaServico = reader.IsDBNull(horaServicoOrdinal)
                        ? null
                        : reader.GetTimeSpan(horaServicoOrdinal).ToString(@"hh\:mm"),

                    valorEstimado = reader.IsDBNull(valorEstimadoOrdinal)
                        ? 0
                        : reader.GetDecimal(valorEstimadoOrdinal),

                    latitude = reader.IsDBNull(latitudeOrdinal)
                        ? (decimal?)null
                        : reader.GetDecimal(latitudeOrdinal),

                    longitude = reader.IsDBNull(longitudeOrdinal)
                        ? (decimal?)null
                        : reader.GetDecimal(longitudeOrdinal),

                    quantidadeProfissionais = Convert.ToInt32(reader.GetValue(quantidadeProfissionaisOrdinal)),
                    totalCandidaturas = Convert.ToInt32(reader.GetValue(totalCandidaturasOrdinal)),
                    totalAceitos = Convert.ToInt32(reader.GetValue(totalAceitosOrdinal)),
                    vagasDisponiveis = Convert.ToInt32(reader.GetValue(vagasDisponiveisOrdinal)),

                    status = reader.GetString(statusOrdinal)
                });
            }

            return Ok(chamados);
        }

        [HttpGet("sugestoes-endereco")]
        public async Task<IActionResult> BuscarSugestoesEndereco(
            [FromQuery] string texto,
            [FromQuery] string? cidade,
            [FromQuery] string? estado
        )
        {
            if (string.IsNullOrWhiteSpace(texto) || texto.Trim().Length < 3)
            {
                return Ok(new List<object>());
            }

            var tomTomApiKey = _configuration["TomTom:ApiKey"];

            if (string.IsNullOrWhiteSpace(tomTomApiKey))
            {
                return BadRequest(new
                {
                    mensagem = "Chave da TomTom não configurada."
                });
            }

            var partesBusca = new List<string>
            {
                texto.Trim()
            };

            if (!string.IsNullOrWhiteSpace(cidade))
                partesBusca.Add(cidade.Trim());

            if (!string.IsNullOrWhiteSpace(estado))
                partesBusca.Add(estado.Trim());

            partesBusca.Add("Brasil");

            var buscaCompleta = string.Join(", ", partesBusca);
            var buscaUrl = WebUtility.UrlEncode(buscaCompleta);

            var url =
                $"https://api.tomtom.com/search/2/search/{buscaUrl}.json" +
                $"?key={tomTomApiKey}&limit=6&countrySet=BR&language=pt-BR";

            try
            {
                using var response = await TomTomHttpClient.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"Erro TomTom sugestões: {response.StatusCode}");
                    return Ok(new List<object>());
                }

                await using var stream = await response.Content.ReadAsStreamAsync();
                using var json = await JsonDocument.ParseAsync(stream);

                if (!json.RootElement.TryGetProperty("results", out var results))
                {
                    return Ok(new List<object>());
                }

                var sugestoes = new List<object>();

                foreach (var item in results.EnumerateArray())
                {
                    string? nome = null;
                    string endereco = "";
                    string? cidadeRetorno = null;
                    string? estadoRetorno = null;

                    if (item.TryGetProperty("poi", out var poi) &&
                        poi.TryGetProperty("name", out var poiName))
                    {
                        nome = poiName.GetString();
                    }

                    if (item.TryGetProperty("address", out var address))
                    {
                        var partesEndereco = new List<string>();

                        if (address.TryGetProperty("freeformAddress", out var freeformAddress))
                        {
                            var textoEndereco = freeformAddress.GetString();

                            if (!string.IsNullOrWhiteSpace(textoEndereco))
                                partesEndereco.Add(textoEndereco);
                        }

                        if (address.TryGetProperty("municipality", out var municipio))
                        {
                            cidadeRetorno = municipio.GetString();

                            if (!string.IsNullOrWhiteSpace(cidadeRetorno))
                                partesEndereco.Add(cidadeRetorno);
                        }

                        if (address.TryGetProperty("countrySubdivision", out var uf))
                        {
                            estadoRetorno = uf.GetString();

                            if (!string.IsNullOrWhiteSpace(estadoRetorno))
                                partesEndereco.Add(estadoRetorno);
                        }

                        endereco = string.Join(" - ", partesEndereco.Where(p => !string.IsNullOrWhiteSpace(p)));

                        if (string.IsNullOrWhiteSpace(nome))
                        {
                            if (address.TryGetProperty("streetName", out var streetName))
                                nome = streetName.GetString();

                            if (string.IsNullOrWhiteSpace(nome) &&
                                address.TryGetProperty("freeformAddress", out var freeformName))
                                nome = freeformName.GetString();
                        }
                    }

                    decimal? latitude = null;
                    decimal? longitude = null;

                    if (item.TryGetProperty("position", out var position))
                    {
                        latitude = (decimal)position.GetProperty("lat").GetDouble();
                        longitude = (decimal)position.GetProperty("lon").GetDouble();
                    }

                    if (!string.IsNullOrWhiteSpace(endereco) && latitude.HasValue && longitude.HasValue)
                    {
                        sugestoes.Add(new
                        {
                            nome,
                            endereco,
                            cidade = cidadeRetorno,
                            estado = estadoRetorno,
                            latitude,
                            longitude
                        });
                    }
                }

                return Ok(sugestoes);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao buscar sugestões de endereço: {ex.Message}");
                return Ok(new List<object>());
            }
        }

        [HttpPost]
        public async Task<IActionResult> CriarChamado([FromBody] CriarChamadoRequest request)
        {
            if (request.EmpresaId <= 0)
                return BadRequest(new { mensagem = "Empresa é obrigatória." });

            if (request.CategoriaId <= 0)
                return BadRequest(new { mensagem = "Categoria é obrigatória." });

            if (string.IsNullOrWhiteSpace(request.Titulo))
                return BadRequest(new { mensagem = "Título é obrigatório." });

            if (string.IsNullOrWhiteSpace(request.Descricao))
                return BadRequest(new { mensagem = "Descrição é obrigatória." });

            if (request.QuantidadeProfissionais <= 0)
                return BadRequest(new { mensagem = "Informe pelo menos 1 profissional." });

            if (request.QuantidadeProfissionais > 99)
                return BadRequest(new { mensagem = "O limite máximo é de 99 profissionais." });

            if (request.ValorEstimado.HasValue && request.ValorEstimado.Value > 1000000m)
            {
                return BadRequest(new
                {
                    mensagem = "O valor estimado não pode ser maior que R$ 1.000.000,00."
                });
            }

            if (request.ValorEstimado.HasValue && request.ValorEstimado.Value < 1m)
            {
                return BadRequest(new
                {
                    mensagem = "O valor estimado deve ser de pelo menos R$ 1,00."
                });
            }

            var connectionString = _configuration.GetConnectionString("DefaultConnection");

            using var connection = new MySqlConnection(connectionString);
            await connection.OpenAsync();

            var coordenadas = await BuscarCoordenadasTomTomAsync(request);

            var query = @"
                INSERT INTO chamados
                (
                    empresa_id,
                    categoria_id,
                    titulo,
                    descricao,
                    local_servico,
                    cidade,
                    estado,
                    data_servico,
                    hora_servico,
                    valor_estimado,
                    latitude,
                    longitude,
                    quantidade_profissionais,
                    status
                )
                VALUES
                (
                    @EmpresaId,
                    @CategoriaId,
                    @Titulo,
                    @Descricao,
                    @LocalServico,
                    @Cidade,
                    @Estado,
                    @DataServico,
                    @HoraServico,
                    @ValorEstimado,
                    @Latitude,
                    @Longitude,
                    @QuantidadeProfissionais,
                    'aberto'
                );

                SELECT LAST_INSERT_ID();
            ";

            using var command = new MySqlCommand(query, connection);

            command.Parameters.AddWithValue("@EmpresaId", request.EmpresaId);
            command.Parameters.AddWithValue("@CategoriaId", request.CategoriaId);
            command.Parameters.AddWithValue("@Titulo", request.Titulo.Trim());
            command.Parameters.AddWithValue("@Descricao", request.Descricao.Trim());

            command.Parameters.AddWithValue(
                "@LocalServico",
                string.IsNullOrWhiteSpace(request.LocalServico)
                    ? DBNull.Value
                    : request.LocalServico.Trim()
            );

            command.Parameters.AddWithValue(
                "@Cidade",
                string.IsNullOrWhiteSpace(request.Cidade)
                    ? DBNull.Value
                    : request.Cidade.Trim()
            );

            command.Parameters.AddWithValue(
                "@Estado",
                string.IsNullOrWhiteSpace(request.Estado)
                    ? DBNull.Value
                    : request.Estado.Trim().ToUpper()
            );

            command.Parameters.AddWithValue(
                "@DataServico",
                request.DataServico.HasValue
                    ? request.DataServico.Value.Date
                    : DBNull.Value
            );

            TimeSpan? horaServico = null;

            if (TimeSpan.TryParse(request.HoraServico, out var hora))
            {
                horaServico = hora;
                command.Parameters.AddWithValue("@HoraServico", hora);
            }
            else
            {
                command.Parameters.AddWithValue("@HoraServico", DBNull.Value);
            }

            command.Parameters.AddWithValue(
                "@ValorEstimado",
                request.ValorEstimado.HasValue
                    ? request.ValorEstimado.Value
                    : DBNull.Value
            );

            command.Parameters.AddWithValue(
                "@Latitude",
                coordenadas.Latitude.HasValue
                    ? (object)coordenadas.Latitude.Value
                    : DBNull.Value
            );

            command.Parameters.AddWithValue(
                "@Longitude",
                coordenadas.Longitude.HasValue
                    ? (object)coordenadas.Longitude.Value
                    : DBNull.Value
            );

            command.Parameters.AddWithValue(
                "@QuantidadeProfissionais",
                request.QuantidadeProfissionais
            );

            var chamadoId = Convert.ToInt32(await command.ExecuteScalarAsync());

            return Created("", new
            {
                mensagem = "Chamado criado com sucesso.",
                chamado = new
                {
                    id = chamadoId,
                    empresaId = request.EmpresaId,
                    categoriaId = request.CategoriaId,
                    titulo = request.Titulo.Trim(),
                    descricao = request.Descricao.Trim(),
                    localServico = request.LocalServico,
                    cidade = request.Cidade,
                    estado = request.Estado,
                    dataServico = request.DataServico?.ToString("yyyy-MM-dd"),
                    horaServico = horaServico.HasValue
                        ? horaServico.Value.ToString(@"hh\:mm")
                        : null,
                    valorEstimado = request.ValorEstimado,
                    latitude = coordenadas.Latitude,
                    longitude = coordenadas.Longitude,
                    quantidadeProfissionais = request.QuantidadeProfissionais,
                    totalCandidaturas = 0,
                    totalAceitos = 0,
                    vagasDisponiveis = request.QuantidadeProfissionais,
                    status = "aberto"
                }
            });
        }

        [HttpPost("{id}/finalizar")]
        public async Task<IActionResult> FinalizarChamado(int id)
        {
            if (id <= 0)
                return BadRequest(new { mensagem = "Chamado inválido." });

            var connectionString = _configuration.GetConnectionString("DefaultConnection");

            using var connection = new MySqlConnection(connectionString);
            await connection.OpenAsync();

            var buscarQuery = @"
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
                WHERE chamados.id = @Id;
            ";

            int quantidadeProfissionais;
            int totalAceitos;
            string statusAtual;

            using (var buscarCommand = new MySqlCommand(buscarQuery, connection))
            {
                buscarCommand.Parameters.AddWithValue("@Id", id);

                using var reader = await buscarCommand.ExecuteReaderAsync();

                if (!await reader.ReadAsync())
                {
                    return NotFound(new
                    {
                        mensagem = "Chamado não encontrado."
                    });
                }

                statusAtual = reader.GetString("status");
                quantidadeProfissionais = Convert.ToInt32(reader["quantidade_profissionais"]);
                totalAceitos = Convert.ToInt32(reader["total_aceitos"]);
            }

            var statusNormalizado = statusAtual.ToLower().Trim();

            if (statusNormalizado == "cancelado" || statusNormalizado == "cancelada")
            {
                return BadRequest(new
                {
                    mensagem = "Não é possível finalizar um chamado cancelado."
                });
            }

            if (
                statusNormalizado == "finalizado" ||
                statusNormalizado == "concluido" ||
                statusNormalizado == "concluído" ||
                statusNormalizado == "finished"
            )
            {
                return BadRequest(new
                {
                    mensagem = "Este chamado já está finalizado."
                });
            }

            if (totalAceitos <= 0)
            {
                return BadRequest(new
                {
                    mensagem = "Não é possível finalizar um chamado sem freelancers aceitos."
                });
            }

            if (totalAceitos < quantidadeProfissionais)
            {
                return BadRequest(new
                {
                    mensagem = "Ainda existem vagas abertas neste chamado. Preencha as vagas antes de finalizar."
                });
            }

            var finalizarQuery = @"
                UPDATE chamados
                SET status = 'finalizado'
                WHERE id = @Id;
            ";

            using (var finalizarCommand = new MySqlCommand(finalizarQuery, connection))
            {
                finalizarCommand.Parameters.AddWithValue("@Id", id);
                await finalizarCommand.ExecuteNonQueryAsync();
            }

            return Ok(new
            {
                mensagem = "Chamado finalizado com sucesso.",
                chamadoId = id,
                quantidadeProfissionais,
                totalAceitos,
                vagasDisponiveis = 0,
                status = "finalizado"
            });
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> CancelarChamado(int id)
        {
            if (id <= 0)
                return BadRequest(new { mensagem = "Chamado inválido." });

            var connectionString = _configuration.GetConnectionString("DefaultConnection");

            using var connection = new MySqlConnection(connectionString);
            await connection.OpenAsync();

            var verificarQuery = @"
                SELECT COUNT(*)
                FROM chamados
                WHERE id = @Id;
            ";

            using (var verificarCommand = new MySqlCommand(verificarQuery, connection))
            {
                verificarCommand.Parameters.AddWithValue("@Id", id);

                var chamadoExiste = Convert.ToInt32(await verificarCommand.ExecuteScalarAsync());

                if (chamadoExiste == 0)
                {
                    return NotFound(new
                    {
                        mensagem = "Chamado não encontrado."
                    });
                }
            }

            var cancelarQuery = @"
                UPDATE chamados
                SET status = 'cancelado'
                WHERE id = @Id;
            ";

            using (var cancelarCommand = new MySqlCommand(cancelarQuery, connection))
            {
                cancelarCommand.Parameters.AddWithValue("@Id", id);
                await cancelarCommand.ExecuteNonQueryAsync();
            }

            return Ok(new
            {
                mensagem = "Chamado cancelado com sucesso."
            });
        }
    }
}