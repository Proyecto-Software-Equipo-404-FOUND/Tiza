import { IlustracionInicio } from './IlustracionInicio';

// Pantalla de bienvenida. Qué ve el alumno acá es una pregunta pendiente para el Product Owner.
export function PaginaInicio() {
  return (
    <main className='relative isolate min-h-[calc(100vh-4.5rem)] overflow-hidden'>
      {/* Manchas de fondo, solo decoración */}
      <div
        aria-hidden='true'
        className='pointer-events-none absolute -right-24 -top-24 -z-10 h-80 w-[28rem] rounded-[60%_40%_55%_45%] bg-gradient-to-br from-tiza-menta to-[#BFE5D3] opacity-80'
      />
      <div
        aria-hidden='true'
        className='pointer-events-none absolute -bottom-32 -left-24 -z-10 h-72 w-[30rem] rounded-[45%_55%_60%_40%] bg-gradient-to-tr from-[#BFE5D3] to-tiza-menta opacity-70'
      />

      <div className='mx-auto grid w-full max-w-6xl items-center gap-8 p-6 md:grid-cols-2 md:p-12 lg:p-16'>
        <div>
          <span className='text-sm font-semibold uppercase tracking-wide text-tiza-pasto'>
            Plataforma de Evaluación Institucional
          </span>
          <h1 className='mb-5 mt-3 font-heading text-4xl leading-tight text-tiza-bosque md:text-5xl lg:text-6xl'>
            Corregí en minutos.
            <br />
            <span className='text-tiza-pasto'>Devolvé el mismo día.</span>
          </h1>
          <p className='max-w-xl text-lg text-tiza-bosque/80'>
            Evaluación en vivo asistida con IA para instituciones educativas. El docente decide la
            nota; la IA sugiere según rúbrica y acelera la devolución pedagógica.
          </p>
        </div>
        <IlustracionInicio className='mx-auto w-full max-w-md md:max-w-none' />
      </div>
    </main>
  );
}
