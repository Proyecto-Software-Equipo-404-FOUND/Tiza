const { defineConfig } = require('cypress');

module.exports = defineConfig({
  video: false,
  screenshotOnRunFailure: false,
  e2e: {
    baseUrl: 'http://127.0.0.1:3000',
    supportFile: false,
    specPattern: 'cypress/e2e/**/*.cy.cjs',
    setupNodeEvents(on, config) {
      config.env.password = process.env.QA_DOCENTE_PASSWORD;
      config.env.email = process.env.QA_DOCENTE_EMAIL || 'docente@a.test';
      config.env.mode = process.env.QA_CYPRESS_MODE || 'live';
      return config;
    },
  },
});
