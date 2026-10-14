import React from 'react'

export default function App() {
  return (
    <div className="min-h-screen flex flex-col justify-between p-6 md:p-12">
      <header className="max-w-5xl mx-auto w-full flex items-center justify-between border-b border-gray-200 pb-4">
        <div className="flex items-center gap-2">
          {/* Logo Tiza con la tiza inclinada de color lima */}
          <span className="text-3xl font-heading font-black tracking-tight text-tiza-pizarra">
            t<span className="text-tiza-lima inline-block transform -rotate-12">ı</span>za
          </span>
          <span className="text-xs bg-tiza-lima/20 text-tiza-pizarra px-2 py-0.5 rounded-full font-semibold border border-tiza-lima">
            PEI MVP
          </span>
        </div>
        <nav className="text-sm font-medium text-gray-600 flex gap-4">
          <span className="text-tiza-violeta">Sprint 1</span>
        </nav>
      </header>

      <main className="max-w-5xl mx-auto w-full my-auto py-12">
        <div className="bg-white rounded-2xl shadow-sm border border-gray-100 p-8 md:p-12">
          <span className="text-tiza-violeta font-semibold text-sm tracking-wide uppercase">
            Plataforma de Evaluación Institucional
          </span>
          <h1 className="text-4xl md:text-5xl font-heading text-tiza-pizarra mt-2 mb-4">
            Corregí en minutos.<br />Devolvé el mismo día.
          </h1>
          <p className="text-lg text-gray-600 max-w-2xl mb-8">
            Evaluación en vivo asistida con IA para instituciones educativas. El docente decide la nota; la IA sugiere según rúbrica y acelera la devolución pedagógica.
          </p>

          <div className="grid grid-cols-1 md:grid-cols-3 gap-4 pt-6 border-t border-gray-100">
            <div className="p-4 rounded-xl bg-tiza-fondo border border-gray-200/60">
              <h3 className="font-heading text-tiza-pizarra text-base mb-1">Cursos & Cohortes</h3>
              <p className="text-xs text-gray-600">Aislamiento por institución (Multi-tenant) y gestión académica.</p>
            </div>
            <div className="p-4 rounded-xl bg-tiza-fondo border border-gray-200/60">
              <h3 className="font-heading text-tiza-pizarra text-base mb-1">Sala en Vivo</h3>
              <p className="text-xs text-gray-600">Tiempo real con SignalR y registro de pérdidas de foco.</p>
            </div>
            <div className="p-4 rounded-xl bg-tiza-fondo border border-gray-200/60">
              <h3 className="font-heading text-tiza-pizarra text-base mb-1">Corrección con IA</h3>
              <p className="text-xs text-gray-600">Sugerencia basada en rúbricas institucionales con servidores MCP.</p>
            </div>
          </div>
        </div>
      </main>

      <footer className="max-w-5xl mx-auto w-full text-center text-xs text-gray-500 pt-6">
        PEI / Tiza · Proyecto de Software · UNAJ 2026
      </footer>
    </div>
  )
}
