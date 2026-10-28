using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PEI.SalaEnVivo.Application.Interfaces;
using PEI.SalaEnVivo.Infrastructure.Persistence;
using StackExchange.Redis;

namespace PEI.SalaEnVivo.Infrastructure.Services
{
    public class SalaCronometroService : ISalaCronometroService
    {
        private readonly pei_sala_db _context;
        private readonly IConnectionMultiplexer? _redis;
        private readonly ILogger<SalaCronometroService> _logger;

        private const string PrefijoClave = "sala:";
        private const string SufijoVencimiento = ":vencimiento";

        public SalaCronometroService(
            pei_sala_db context,
            ILogger<SalaCronometroService> logger,
            IConnectionMultiplexer? redis = null)
        {
            _context = context;
            _logger = logger;
            _redis = redis;
        }

        public async Task EstablecerVencimientoAsync(
            Guid salaId,
            DateTime fechaVencimientoUtc,
            CancellationToken cancellationToken = default)
        {
            if (_redis != null && _redis.IsConnected)
            {
                try
                {
                    var db = _redis.GetDatabase();
                    var clave = $"{PrefijoClave}{salaId}{SufijoVencimiento}";
                    var valor = fechaVencimientoUtc.ToString("O", CultureInfo.InvariantCulture);

                    var tiempoRestante = fechaVencimientoUtc - DateTime.UtcNow;
                    // Mantener en Redis hasta el vencimiento + margen de 1 hora para reintentos / consultas
                    var ttl = tiempoRestante > TimeSpan.Zero ? tiempoRestante + TimeSpan.FromHours(1) : TimeSpan.FromHours(1);

                    await db.StringSetAsync(clave, valor, ttl);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "No se pudo persistir el vencimiento de la sala {SalaId} en Redis. Fallback a base de datos.", salaId);
                }
            }
        }

        public async Task<DateTime?> ObtenerVencimientoAsync(
            Guid salaId,
            CancellationToken cancellationToken = default)
        {
            // 1. Intentar leer desde Redis (latencia sub-milisegundo)
            if (_redis != null && _redis.IsConnected)
            {
                try
                {
                    var db = _redis.GetDatabase();
                    var clave = $"{PrefijoClave}{salaId}{SufijoVencimiento}";
                    var valorRedis = await db.StringGetAsync(clave);

                    if (valorRedis.HasValue && DateTime.TryParse(
                        valorRedis.ToString(),
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.RoundtripKind,
                        out var fechaRedis))
                    {
                        return fechaRedis;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error al leer vencimiento de Redis para sala {SalaId}. Fallback a PostgreSQL.", salaId);
                }
            }

            // 2. Fallback a PostgreSQL
            var sala = await _context.Sala
                .AsNoTracking()
                .Where(s => s.Id == salaId)
                .Select(s => new { s.FechaVencimiento })
                .FirstOrDefaultAsync(cancellationToken);

            if (sala == null)
            {
                return null;
            }

            // Repoblar en Redis para subsiguientes lecturas si está disponible
            await EstablecerVencimientoAsync(salaId, sala.FechaVencimiento, cancellationToken);

            return sala.FechaVencimiento;
        }

        public async Task<bool> EstaVencidaAsync(
            Guid salaId,
            DateTime nowUtc,
            CancellationToken cancellationToken = default)
        {
            var vencimiento = await ObtenerVencimientoAsync(salaId, cancellationToken);
            if (!vencimiento.HasValue)
            {
                return true; // Si no existe la sala o no tiene fecha, tratar como no disponible/vencida
            }

            return nowUtc > vencimiento.Value;
        }
    }
}
