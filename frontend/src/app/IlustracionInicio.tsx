// Ilustración de la pantalla de inicio: libros, hoja con lista de tareas y un lápiz que escribe.
// Es un SVG propio, sin archivos externos. Las animaciones están en index.css (clases `inicio-*`)
// y solo corren si la persona no pidió reducir el movimiento.

const ESTRELLA = 'M0 -14 Q2 -2 14 0 Q2 2 0 14 Q-2 2 -14 0 Q-2 -2 0 -14Z';

function Estrella({ x, y, escala, variante }: { x: number; y: number; escala: number; variante: 1 | 2 | 3 | 4 }) {
  return (
    <g transform={`translate(${x} ${y}) scale(${escala})`}>
      <path d={ESTRELLA} fill='#F6C85F' className={`inicio-estrella inicio-estrella-${variante}`} />
    </g>
  );
}

function Fila({ y, n }: { y: number; n: 1 | 2 | 3 }) {
  return (
    <g>
      <circle cx='232' cy={y} r='18' fill='#F3F8F6' stroke='#7CC9AE' strokeWidth='3' />
      {/* El tilde lo va dibujando el lápiz: ver `inicio-tilde-*` en index.css */}
      <path
        d={`M223 ${y} l6 7 l12 -14`}
        pathLength='100'
        fill='none'
        stroke='#2F9E75'
        strokeWidth='5'
        strokeLinecap='round'
        strokeLinejoin='round'
        className={`inicio-tilde inicio-tilde-${n}`}
      />
      <rect x='268' y={y - 12} width='124' height='9' rx='4.5' fill='#DCE3EE' />
      <rect x='268' y={y + 3} width='88' height='9' rx='4.5' fill='#E7ECF4' />
    </g>
  );
}

export function IlustracionInicio({ className = '' }: { className?: string }) {
  return (
    <svg
      viewBox='0 -30 520 450'
      className={className}
      aria-hidden='true'
      focusable='false'
      data-testid='ilustracion-inicio'
    >
      <ellipse cx='265' cy='384' rx='210' ry='20' fill='#BFE5D3' opacity='0.7' />

      {/* Hoja con la lista: los tres tildes los dibuja el lápiz */}
      <g transform='rotate(6 310 190)'>
        <rect x='190' y='40' width='240' height='318' rx='18' fill='#FFFFFF' stroke='#E3E8EE' strokeWidth='2' />
        <Fila y={100} n={1} />
        <Fila y={158} n={2} />
        <Fila y={216} n={3} />
        {/* La punta del lápiz es el origen (0,0) del grupo: así gira y se mueve sobre la línea */}
        <g transform='translate(330 260)'>
          <g className='inicio-lapiz-avance'>
            <g className='inicio-lapiz-trazo'>
              <g transform='rotate(28)'>
                <path d='M0 0 L-11 -28 L11 -28 Z' fill='#F2C98A' />
                <path d='M0 0 L-4 -10 L4 -10 Z' fill='#123F36' />
                <rect x='-11' y='-112' width='22' height='84' fill='#3FAE86' />
                <rect x='-11' y='-112' width='8' height='84' fill='#2F7D5B' />
                <rect x='-11' y='-126' width='22' height='16' rx='6' fill='#2F7D5B' />
                <rect x='-11' y='-120' width='22' height='9' fill='#EEF3F1' />
              </g>
            </g>
          </g>
        </g>
      </g>

      {/* Libros */}
      <g>
        <rect x='26' y='322' width='178' height='40' rx='7' fill='#7B64C9' />
        <rect x='36' y='329' width='164' height='26' fill='#FFFFFF' />
        <rect x='36' y='342' width='164' height='2' fill='#E3E8EE' />
        <rect x='44' y='276' width='156' height='46' rx='7' fill='#2F7D5B' />
        <rect x='54' y='284' width='144' height='30' fill='#FFFFFF' />
        <rect x='54' y='298' width='144' height='2' fill='#E3E8EE' />
        <path d='M44 276 Q122 254 200 276 L200 284 Q122 264 44 284 Z' fill='#4FB08C' />
      </g>

      {/* Chispas a la izquierda de la hoja: salen en abanico y crecen y se achican */}
      <g transform='translate(184 60)' stroke='#2F9E75' strokeWidth='7' strokeLinecap='round' fill='none'>
        {[-168, -138, -108].map((angulo, i) => (
          <g key={angulo} className={`inicio-chispa inicio-chispa-${i + 1}`}>
            <g transform={`rotate(${angulo})`}>
              <line x1='24' y1='0' x2='44' y2='0' />
            </g>
          </g>
        ))}
      </g>

      <Estrella x={120} y={96} escala={1} variante={1} />
      <Estrella x={160} y={206} escala={0.75} variante={2} />
      <Estrella x={486} y={304} escala={1.1} variante={3} />
      <Estrella x={452} y={100} escala={0.7} variante={4} />
    </svg>
  );
}
