import { BookOpen, FlaskConical, GraduationCap, Landmark, Languages, Sigma, type LucideIcon } from 'lucide-react';

type Estilo = { Icono: LucideIcon; fondo: string; color: string };

// El área es texto libre en el backend: se reconoce por palabras clave y, si no, se usa un ícono neutro
const estilos: Array<[RegExp, Estilo]> = [
  [/matem|calcul|algebra/i, { Icono: Sigma, fondo: 'bg-amber-100', color: 'text-amber-700' }],
  [/lengua|liter|letras/i, { Icono: BookOpen, fondo: 'bg-rose-100', color: 'text-rose-700' }],
  [/cienc|fisic|quimic|biolog/i, { Icono: FlaskConical, fondo: 'bg-emerald-100', color: 'text-emerald-700' }],
  [/idioma|ingl|franc|portug/i, { Icono: Languages, fondo: 'bg-sky-100', color: 'text-sky-700' }],
  [/histor|geograf|social/i, { Icono: Landmark, fondo: 'bg-orange-100', color: 'text-orange-700' }],
];

const neutro: Estilo = { Icono: GraduationCap, fondo: 'bg-tiza-menta', color: 'text-tiza-pasto' };

export function IconoArea({ area }: { area: string }) {
  const { Icono, fondo, color } = estilos.find(([patron]) => patron.test(area))?.[1] ?? neutro;
  return (
    <span className={`flex h-10 w-10 shrink-0 items-center justify-center rounded-xl ${fondo} ${color}`}>
      <Icono className='h-5 w-5' aria-hidden='true' />
    </span>
  );
}
