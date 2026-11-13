namespace PEI.SalaEnVivo.Application.Interfaces
{
    /// <summary>
    /// Consultas de Sala en Vivo al microservicio de Cursos.
    /// </summary>
    public interface ICursosClient
    {
        /// <summary>
        /// Indica si el alumno tiene una matrícula activa en la cohorte
        /// (GET /api/v1/cohortes/{cohorteId}/matriculas/alumnos/{alumnoId}).
        /// Se reenvía el token del propio alumno: Cursos solo le deja consultarse a sí mismo.
        /// Retorna false ante 404 o 403. Lanza HttpRequestException si Cursos no responde o falla.
        /// </summary>
        Task<bool> AlumnoTieneMatriculaActivaAsync(
            Guid cohorteId,
            Guid alumnoId,
            string tokenAlumno,
            CancellationToken cancellationToken = default);
    }
}
