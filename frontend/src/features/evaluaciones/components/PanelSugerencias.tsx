import { useState, type KeyboardEvent } from 'react';
import { ApiError } from '@/shared/api/cliente';
import { BannerError } from '@/shared/components/BannerError';
import { Boton } from '@/shared/components/Boton';
import { Campo } from '@/shared/components/Campo';
import { CampoSelect } from '@/shared/components/CampoSelect';
import { Spinner } from '@/shared/components/Spinner';
import { Tarjeta } from '@/shared/components/Tarjeta';
import type { PreguntaBorrador } from '../borrador';
import { useApuntesParaSugerir } from '../hooks/useApuntesParaSugerir';
import { useCursosParaSugerir } from '../hooks/useCursosParaSugerir';
import { useSugerirPreguntas } from '../hooks/useSugerirPreguntas';
import {
  CANTIDAD_MAXIMA,
  CANTIDAD_MINIMA,
  CANTIDAD_POR_DEFECTO,
  TEMA_MAXIMO,
  borradorDesdeSugerida,
  mensajeDeSugerencia,
  type TipoPregunta,
} from '../sugerencias';

type Props = {
  onAgregar: (preguntas: PreguntaBorrador[]) => void;
  onCerrar: () => void;
  deshabilitado: boolean;
};

type ErroresPanel = { curso?: string; apunte?: string; cantidad?: string; tipos?: string; tema?: string };

// El panel vive dentro del <form> del examen: el Enter en un campo no tiene que guardar el borrador
const evitarEnvio = (evento: KeyboardEvent<HTMLDivElement>) => {
  if (evento.key === 'Enter' && evento.target instanceof HTMLInputElement) evento.preventDefault();
};

export function PanelSugerencias({ onAgregar, onCerrar, deshabilitado }: Props) {
  const [cursoId, setCursoId] = useState('');
  const [materialId, setMaterialId] = useState('');
  const [cantidad, setCantidad] = useState(String(CANTIDAD_POR_DEFECTO));
  const [opcionMultiple, setOpcionMultiple] = useState(true);
  const [abierta, setAbierta] = useState(true);
  const [tema, setTema] = useState('');
  const [errores, setErrores] = useState<ErroresPanel>({});
  const [elegidas, setElegidas] = useState<number[]>([]);

  const cursos = useCursosParaSugerir(true);
  const apuntes = useApuntesParaSugerir(cursoId);
  const sugerir = useSugerirPreguntas();

  const apuntesListos = (apuntes.data ?? []).filter((apunte) => apunte.estado === 'Listo');
  const hayApuntesSinListar = (apuntes.data ?? []).length > apuntesListos.length;

  const bloqueado = deshabilitado || sugerir.isPending;

  const pedir = () => {
    const encontrados: ErroresPanel = {};
    const cantidadNumero = Number(cantidad);
    if (!cursoId) encontrados.curso = 'Elegí un curso';
    if (!materialId) encontrados.apunte = 'Elegí un apunte';
    if (!Number.isInteger(cantidadNumero) || cantidadNumero < CANTIDAD_MINIMA || cantidadNumero > CANTIDAD_MAXIMA)
      encontrados.cantidad = `Ingresá un número entero entre ${CANTIDAD_MINIMA} y ${CANTIDAD_MAXIMA}`;
    if (!opcionMultiple && !abierta) encontrados.tipos = 'Elegí al menos un tipo de pregunta';
    if (tema.trim().length > TEMA_MAXIMO) encontrados.tema = `Máximo ${TEMA_MAXIMO} caracteres`;
    setErrores(encontrados);
    if (Object.keys(encontrados).length > 0) return;

    const tipos: TipoPregunta[] = [];
    if (opcionMultiple) tipos.push('OpcionMultiple');
    if (abierta) tipos.push('Abierta');

    sugerir.mutate(
      { materialId, cantidad: cantidadNumero, tipos, ...(tema.trim() ? { tema: tema.trim() } : {}) },
      { onSuccess: (respuesta) => setElegidas(respuesta.preguntas.map((_, indice) => indice)) },
    );
  };

  const alternar = (indice: number) =>
    setElegidas((actuales) =>
      actuales.includes(indice) ? actuales.filter((i) => i !== indice) : [...actuales, indice],
    );

  const agregar = () => {
    if (!sugerir.data) return;
    const elegidasEnOrden = sugerir.data.preguntas.filter((_, indice) => elegidas.includes(indice));
    onAgregar(elegidasEnOrden.map(borradorDesdeSugerida));
    onCerrar();
  };

  // Reintentar sirve ante una caída o un 5xx; un apunte sin texto o inexistente no mejora repitiendo
  const reintentable = !(sugerir.error instanceof ApiError) || sugerir.error.estado >= 500;

  return (
    // eslint-disable-next-line jsx-a11y/no-static-element-interactions
    <div onKeyDown={evitarEnvio}>
      <Tarjeta
        titulo='Sugerir preguntas con IA'
        subtitulo='La IA arma preguntas a partir de un apunte del curso. Las revisás y elegís cuáles agregar: nada se guarda hasta que guardes el examen.'
      >
        <div className='flex flex-col gap-5'>
          <div className='grid gap-4 sm:grid-cols-2'>
            <CampoSelect
              etiqueta='Curso'
              value={cursoId}
              onChange={(evento) => {
                setCursoId(evento.target.value);
                setMaterialId('');
                setElegidas([]);
                sugerir.reset();
              }}
              error={errores.curso}
              disabled={bloqueado || cursos.isPending}
            >
              <option value=''>Elegí un curso</option>
              {(cursos.data?.items ?? []).map((curso) => (
                <option key={curso.id} value={curso.id}>
                  {curso.nombre}
                </option>
              ))}
            </CampoSelect>
            <CampoSelect
              etiqueta='Apunte'
              value={materialId}
              onChange={(evento) => {
                setMaterialId(evento.target.value);
                setElegidas([]);
                sugerir.reset();
              }}
              error={errores.apunte}
              disabled={bloqueado || !cursoId || apuntes.isPending}
            >
              <option value=''>Elegí un apunte</option>
              {apuntesListos.map((apunte) => (
                <option key={apunte.id} value={apunte.id}>
                  {apunte.titulo}
                </option>
              ))}
            </CampoSelect>
          </div>

          {cursos.isError && <BannerError mensaje={mensajeDeSugerencia(cursos.error)} onReintentar={() => cursos.refetch()} />}
          {apuntes.isError && <BannerError mensaje={mensajeDeSugerencia(apuntes.error)} onReintentar={() => apuntes.refetch()} />}
          {cursoId && apuntes.isSuccess && apuntesListos.length === 0 && (
            <p className='text-sm text-tiza-bosque/70'>
              Este curso no tiene apuntes listos todavía. Subí un PDF en la sección de apuntes del curso.
            </p>
          )}
          {hayApuntesSinListar && apuntesListos.length > 0 && (
            <p className='text-sm text-tiza-bosque/70'>
              Solo se muestran los apuntes en estado «Listo»; los que se están procesando o tienen error no se pueden usar.
            </p>
          )}

          <div className='grid gap-4 sm:grid-cols-2'>
            <Campo
              etiqueta='Cantidad de preguntas'
              type='number'
              min={CANTIDAD_MINIMA}
              max={CANTIDAD_MAXIMA}
              value={cantidad}
              onChange={(evento) => setCantidad(evento.target.value)}
              error={errores.cantidad}
              disabled={bloqueado}
            />
            <Campo
              etiqueta='Tema (opcional)'
              value={tema}
              onChange={(evento) => setTema(evento.target.value)}
              error={errores.tema}
              disabled={bloqueado}
              placeholder='Ej.: Estructuras de control'
            />
          </div>

          <fieldset className='flex flex-col gap-2'>
            <legend className='mb-1 text-sm font-semibold text-tiza-bosque'>Tipos de pregunta</legend>
            <label className='flex items-center gap-2 text-tiza-bosque'>
              <input
                type='checkbox'
                checked={opcionMultiple}
                onChange={(evento) => setOpcionMultiple(evento.target.checked)}
                disabled={bloqueado}
                className='h-4 w-4 accent-tiza-pasto'
              />
              Opción múltiple
            </label>
            <label className='flex items-center gap-2 text-tiza-bosque'>
              <input
                type='checkbox'
                checked={abierta}
                onChange={(evento) => setAbierta(evento.target.checked)}
                disabled={bloqueado}
                className='h-4 w-4 accent-tiza-pasto'
              />
              Abierta
            </label>
            {errores.tipos && (
              <p role='alert' className='text-sm text-tiza-coral'>
                {errores.tipos}
              </p>
            )}
          </fieldset>

          <div className='flex flex-wrap items-center gap-3'>
            <Boton type='button' onClick={pedir} cargando={sugerir.isPending} disabled={bloqueado}>
              Pedir sugerencias
            </Boton>
            <Boton type='button' variante='secundario' onClick={onCerrar} disabled={sugerir.isPending}>
              Cerrar
            </Boton>
            <p className='text-sm text-tiza-bosque/70'>Puede tardar hasta un minuto.</p>
          </div>

          {sugerir.isPending && <Spinner texto='La IA está armando las preguntas… puede tardar hasta un minuto.' />}

          {sugerir.isError && !(sugerir.error instanceof ApiError && sugerir.error.estado === 403) && (
            <BannerError
              mensaje={mensajeDeSugerencia(sugerir.error)}
              onReintentar={reintentable ? pedir : undefined}
            />
          )}

          {sugerir.data && (
            <div className='flex flex-col gap-4'>
              {sugerir.data.preguntas.length === 0 ? (
                <p className='text-tiza-bosque/80'>La IA no devolvió preguntas para este apunte. Probá con otro tema o apunte.</p>
              ) : (
                <>
                  <p className='text-sm text-tiza-bosque/70'>
                    Revisá cada pregunta contra el fragmento del apunte antes de agregarla.
                    {sugerir.data.revisadaPorAgenteRevisor ? ' Pasaron por un agente revisor.' : ''}
                  </p>
                  <ol className='flex flex-col gap-3'>
                    {sugerir.data.preguntas.map((pregunta, indice) => (
                      <li key={indice} className='rounded-2xl border border-tiza-borde bg-white p-4'>
                        <label className='flex items-start gap-3'>
                          <input
                            type='checkbox'
                            checked={elegidas.includes(indice)}
                            onChange={() => alternar(indice)}
                            disabled={deshabilitado}
                            aria-label={`Agregar la pregunta sugerida ${indice + 1}`}
                            className='mt-1 h-4 w-4 shrink-0 accent-tiza-pasto'
                          />
                          <span className='flex flex-col gap-2'>
                            <span className='font-semibold text-tiza-bosque'>{pregunta.enunciado}</span>
                            <span className='text-sm text-tiza-bosque/70'>
                              {pregunta.tipo === 'OpcionMultiple' ? 'Opción múltiple' : 'Abierta'} · {pregunta.puntajeMaximo}{' '}
                              {pregunta.puntajeMaximo === 1 ? 'punto' : 'puntos'}
                            </span>
                            {pregunta.opciones && pregunta.opciones.length > 0 && (
                              <ul className='list-disc pl-5 text-sm text-tiza-bosque'>
                                {pregunta.opciones.map((opcion, posicion) => (
                                  <li key={posicion}>
                                    {opcion.texto}
                                    {opcion.esCorrecta ? ' (correcta)' : ''}
                                  </li>
                                ))}
                              </ul>
                            )}
                            {pregunta.rubrica?.criterios && pregunta.rubrica.criterios.length > 0 && (
                              <ul className='list-disc pl-5 text-sm text-tiza-bosque'>
                                {pregunta.rubrica.criterios.map((criterio, posicion) => (
                                  <li key={posicion}>
                                    {criterio.descripcion} ({criterio.puntaje})
                                  </li>
                                ))}
                              </ul>
                            )}
                            {pregunta.fragmentoApunte && (
                              <span className='rounded-xl bg-tiza-fondo/60 p-3 text-sm italic text-tiza-bosque/80'>
                                Del apunte: {pregunta.fragmentoApunte}
                              </span>
                            )}
                          </span>
                        </label>
                      </li>
                    ))}
                  </ol>
                  <Boton type='button' onClick={agregar} disabled={deshabilitado || elegidas.length === 0}>
                    {elegidas.length === 1 ? 'Agregar la pregunta elegida' : `Agregar las ${elegidas.length} elegidas`}
                  </Boton>
                </>
              )}
            </div>
          )}
        </div>
      </Tarjeta>
    </div>
  );
}
