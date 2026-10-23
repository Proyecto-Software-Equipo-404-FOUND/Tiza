// Pantalla de bienvenida. Qué ve el alumno acá es una pregunta pendiente para el Product Owner.
export function PaginaInicio() {
  return (
    <main className='mx-auto w-full max-w-5xl p-6 md:p-12'>
      <div className='rounded-2xl border border-gray-100 bg-white p-8 shadow-sm md:p-12'>
        <span className='text-sm font-semibold uppercase tracking-wide text-tiza-violeta'>
          Plataforma de Evaluación Institucional
        </span>
        <h1 className='mb-4 mt-2 font-heading text-4xl text-tiza-pizarra md:text-5xl'>
          Corregí en minutos.
          <br />
          Devolvé el mismo día.
        </h1>
        <p className='max-w-2xl text-lg text-gray-600'>
          Evaluación en vivo asistida con IA para instituciones educativas. El docente decide la
          nota; la IA sugiere según rúbrica y acelera la devolución pedagógica.
        </p>
      </div>
    </main>
  );
}
