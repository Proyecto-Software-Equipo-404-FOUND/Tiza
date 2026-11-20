// Sin intercepts ni respuestas fabricadas. El modo UI usa el MSW ya existente
// en el frontend; live requiere los servicios y semillas reales (ver README).
const campo = (label) => cy.contains('label', label).invoke('attr', 'for')
  .then(id => cy.get(`[id="${id}"]`));

describe('S2-QA-01: curso → cohorte → matrícula', () => {
  const permisos = Cypress.env('flow') === 'permisos';
  it(permisos ? 'administrador crea y docente matricula con permisos vigentes' : 'docente crea y matricula: criterio original pendiente del PO', () => {
    const mode = Cypress.env('mode');
    const alumnoNombre = mode === 'live' ? 'Alumno uno Prueba A' : 'Alumno 1 A';
    expect(['ui', 'live']).to.include(mode);
    const password = Cypress.env('password');
    expect(password, 'QA_DOCENTE_PASSWORD debe venir del entorno').to.be.a('string').and.not.be.empty;
    cy.visit('/login');
    campo('Correo').should('be.visible');
    // Impide presentar accidentalmente una ejecución con MSW como E2E real.
    cy.window().then(async win => {
      const registrations = await win.navigator.serviceWorker.getRegistrations();
      const mocked = registrations.some(r => r.active?.scriptURL.includes('mockServiceWorker'));
      expect(mocked, 'MSW sólo en modo ui').to.equal(mode === 'ui');
    });
    campo('Correo').type(Cypress.env('email'));
    campo('Contraseña').type(password, { log: false });
    cy.contains('button', /^Ingresar$/).click();
    cy.location('pathname').should('equal', '/');
    cy.get('nav[aria-label="Principal"]').contains('a', /^Cursos$/).click();
    cy.location('pathname').should('equal', '/cursos');
    const cambiarSesion = (email) => {
      // Logout real y navegación SPA: conserva los datos MSW en modo UI.
      cy.contains('button', /^Salir$/).click();
      cy.location('pathname').should('equal', '/login');
      campo('Correo').type(email);
      campo('Contraseña').type(password, { log: false });
      cy.contains('button', /^Ingresar$/).click();
      cy.location('pathname').should('equal', '/');
      cy.get('nav[aria-label="Principal"]').contains('a', /^Cursos$/).click();
      cy.location('pathname').should('equal', '/cursos');
    };
    if (permisos) {
      cy.contains('button', /^Crear curso$/).should('not.exist');
      cy.contains('tr', 'Programación I').contains('a', 'Ver cohortes').click();
      cy.contains('button', /^Crear cohorte$/).should('not.exist');
      // Los POST prohibidos se verifican contra las APIs reales en live-scenarios.
      cambiarSesion('admin@a.test');
    }
    // Nombres fijos: usar una instancia descartable/restablecida en cada ejecución.
    campo('Nombre del curso').type('S2-QA-01 Curso reproducible');
    campo('Área').type('Sistemas');
    cy.contains('button', /Crear curso/).click();
    cy.contains('tr', 'S2-QA-01 Curso reproducible').within(() => {
      cy.contains('a', 'Ver cohortes').click();
    });
    cy.location('pathname').should('match', /^\/cursos\/[\da-f-]+\/cohortes$/);
    campo('Nombre de la cohorte').type('S2-QA-01 Cohorte');
    campo('Período').type('2026-2C');
    cy.get('#docente-cohorte').select('11111111-0000-0000-0000-000000000002');
    cy.contains('button', /^Crear cohorte$/).click();
    cy.contains('tr', 'S2-QA-01 Cohorte').within(() => {
      cy.contains('a', 'Ver matrícula').click();
    });
    cy.location('pathname').should('match', /^\/cohortes\/[\da-f-]+\/matriculas$/);
    if (permisos) {
      cambiarSesion(Cypress.env('email'));
      cy.contains('tr', 'S2-QA-01 Curso reproducible').contains('a', 'Ver cohortes').click();
      cy.contains('tr', 'S2-QA-01 Cohorte').contains('a', 'Ver matrícula').click();
    }
    cy.get('#alumno').select('11111111-0000-0000-0000-000000000003');
    cy.contains('button', /^Matricular$/).click();
    cy.contains('tr', alumnoNombre).should('contain.text', 'Activa');
    if (mode === 'live') {
      // Recarga completa: verifica sesión y datos persistidos fuera de React Query.
      cy.reload();
      cy.location('pathname').should('match', /^\/cohortes\/[\da-f-]+\/matriculas$/);
      cy.contains('tr', alumnoNombre).should('contain.text', 'Activa');
      cy.get('tbody tr').filter(`:contains("${alumnoNombre}")`).should('have.length', 1);
    }
    cy.get('tbody tr').filter(`:contains("${alumnoNombre}")`).should('have.length', 1);
    cy.get('#alumno option[value="11111111-0000-0000-0000-000000000003"]').should('not.exist');
    // Volver por navegación conserva el servidor/MSW y fuerza otra consulta de matrícula.
    cy.contains('a', 'Volver a cursos').click();
    cy.contains('tr', 'S2-QA-01 Curso reproducible').contains('a', 'Ver cohortes').click();
    cy.contains('tr', 'S2-QA-01 Cohorte').contains('a', 'Ver matrícula').click();
    cy.contains('tr', alumnoNombre).should('contain.text', 'Activa');
  });
});
