import { FileText, Plus } from 'lucide-react';
import { useState, type FormEvent } from 'react';
import { ApiError } from '@/shared/api/cliente';
import { mensajeDeError } from '@/shared/api/errores';
import { BannerError } from '@/shared/components/BannerError';
import { Boton } from '@/shared/components/Boton';
import { Campo } from '@/shared/components/Campo';
import { CampoSelect } from '@/shared/components/CampoSelect';
import { Tarjeta } from '@/shared/components/Tarjeta';
import {
  TIPOS_EVALUACION,
  aDatosEvaluacion,
  nuevaPregunta,
  validar,
  type DatosEvaluacion,
  type ErroresBorrador,
  type EvaluacionBorrador,
} from '../borrador';
import { EditorPregunta } from './EditorPregunta';

type Props = {
  inicial: EvaluacionBorrador;
  textoEnviar: string;
  enviando: boolean;
  /** Error de la última petición (si la hubo) */
  error: unknown;
  onEnviar: (datos: DatosEvaluacion) => void;
  onCancelar: () => void;
  /** Ante un 409 los datos que se ven están desactualizados: se ofrece volver a pedirlos */
  onRecargar?: () => void;
};

export function FormularioEvaluacion({ inicial, textoEnviar, enviando, error, onEnviar, onCancelar, onRecargar }: Props) {
  const [borrador, setBorrador] = useState<EvaluacionBorrador>(inicial);
  const [errores, setErrores] = useState<ErroresBorrador>({});

  // Si la evaluación ya tiene un tipo fuera de la lista (datos viejos), se conserva como opción
  const tipos: string[] = TIPOS_EVALUACION.includes(borrador.tipo as (typeof TIPOS_EVALUACION)[number])
    ? [...TIPOS_EVALUACION]
    : [...TIPOS_EVALUACION, borrador.tipo];

  const enviar = (evento: FormEvent<HTMLFormElement>) => {
    evento.preventDefault();
    const encontrados = validar(borrador);
    setErrores(encontrados);
    if (Object.keys(encontrados).length > 0) return;
    onEnviar(aDatosEvaluacion(borrador));
  };

  const hayConflicto = error instanceof ApiError && error.estado === 409;
  const hayErrores = Object.keys(errores).length > 0;

  return (
    <form onSubmit={enviar} noValidate className='flex flex-col gap-6'>
      <Tarjeta titulo='Datos generales'>
        <div className='flex flex-col gap-5'>
          <Campo
            etiqueta='Título'
            icono={<FileText className='h-5 w-5' aria-hidden='true' />}
            value={borrador.titulo}
            onChange={(evento) => setBorrador({ ...borrador, titulo: evento.target.value })}
            error={errores.titulo}
            disabled={enviando}
          />
          <CampoSelect
            etiqueta='Tipo'
            value={borrador.tipo}
            onChange={(evento) => setBorrador({ ...borrador, tipo: evento.target.value })}
            disabled={enviando}
          >
            {tipos.map((tipo) => (
              <option key={tipo} value={tipo}>
                {tipo}
              </option>
            ))}
          </CampoSelect>
        </div>
      </Tarjeta>

      <Tarjeta titulo='Preguntas' subtitulo='Podés guardar el borrador sin preguntas y completarlo después.'>
        {borrador.preguntas.length === 0 ? (
          <p className='mb-4 text-tiza-bosque/70'>Todavía no agregaste preguntas.</p>
        ) : (
          <ol className='mb-4 flex flex-col gap-4'>
            {borrador.preguntas.map((pregunta, indice) => (
              <EditorPregunta
                key={pregunta.id}
                pregunta={pregunta}
                numero={indice + 1}
                errores={errores}
                deshabilitado={enviando}
                onCambiar={(cambiada) =>
                  setBorrador({
                    ...borrador,
                    preguntas: borrador.preguntas.map((p) => (p.id === pregunta.id ? cambiada : p)),
                  })
                }
                onQuitar={() =>
                  setBorrador({ ...borrador, preguntas: borrador.preguntas.filter((p) => p.id !== pregunta.id) })
                }
              />
            ))}
          </ol>
        )}
        <Boton
          type='button'
          variante='secundario'
          onClick={() => setBorrador({ ...borrador, preguntas: [...borrador.preguntas, nuevaPregunta()] })}
          disabled={enviando}
        >
          <Plus className='mr-2 inline h-4 w-4' aria-hidden='true' />
          Agregar pregunta
        </Boton>
      </Tarjeta>

      {hayErrores && <BannerError mensaje='Revisá los campos marcados antes de guardar.' />}
      {Boolean(error) && (
        <BannerError
          mensaje={mensajeDeError(error)}
          onReintentar={hayConflicto ? onRecargar : undefined}
          textoBoton='Recargar'
        />
      )}

      <div className='flex flex-wrap gap-3'>
        <Boton type='submit' cargando={enviando}>
          {textoEnviar}
        </Boton>
        <Boton type='button' variante='secundario' onClick={onCancelar} disabled={enviando}>
          Cancelar
        </Boton>
      </div>
    </form>
  );
}
