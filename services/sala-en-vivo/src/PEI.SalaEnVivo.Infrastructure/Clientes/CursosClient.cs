using System.Net;
using System.Net.Http.Headers;
using PEI.SalaEnVivo.Application.Interfaces;

namespace PEI.SalaEnVivo.Infrastructure.Clientes
{
    public sealed class CursosClient : ICursosClient
    {
        private readonly HttpClient _httpClient;

        public CursosClient(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<bool> AlumnoTieneMatriculaActivaAsync(
            Guid cohorteId,
            Guid alumnoId,
            string tokenAlumno,
            CancellationToken cancellationToken = default)
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"api/v1/cohortes/{cohorteId}/matriculas/alumnos/{alumnoId}");

            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenAlumno);

            using var response = await _httpClient.SendAsync(request, cancellationToken);

            if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden)
            {
                return false;
            }

            response.EnsureSuccessStatusCode();
            return true;
        }
    }
}
