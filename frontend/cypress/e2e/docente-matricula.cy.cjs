// Sin intercepts ni respuestas fabricadas. El modo UI usa el MSW ya existente
// en el frontend; live requiere los servicios y semillas reales (ver README).
const campo = (label) => cy.contains('label', label).invoke('attr', 'for')
  .then(id => cy.get(`[id="${id}"]`));

describe('S2-QA-01: docente → curso → cohorte → matrícula', () => {
  it('muestra exactamente una matrícula activa del alumno seleccionado', () => {
    const mode = Cypress.env('mode');
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
    cy.location('pathname').should('equal', '/cursos');
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
    cy.get('#alumno').select('11111111-0000-0000-0000-000000000003');
    cy.contains('button', /^Matricular$/).click();
    cy.contains('tr', 'Alumno 1 A').should('contain.text', 'Activa');
    cy.get('tbody tr').filter(':contains("Alumno 1 A")').should('have.length', 1);
    cy.get('#alumno option[value="11111111-0000-0000-0000-000000000003"]').should('not.exist');
    // Volver por navegación conserva el servidor/MSW y fuerza otra consulta de matrícula.
    cy.contains('a', 'Volver a cursos').click();
    cy.contains('tr', 'S2-QA-01 Curso reproducible').contains('a', 'Ver cohortes').click();
    cy.contains('tr', 'S2-QA-01 Cohorte').contains('a', 'Ver matrícula').click();
    cy.contains('tr', 'Alumno 1 A').should('contain.text', 'Activa');
  });
});
