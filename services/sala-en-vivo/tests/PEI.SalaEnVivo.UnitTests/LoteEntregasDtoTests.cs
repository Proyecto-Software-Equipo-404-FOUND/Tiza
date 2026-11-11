using System;
using System.Collections.Generic;
using System.Text.Json;
using PEI.SalaEnVivo.Application.DTOs.Entregas;
using Xunit;

namespace PEI.SalaEnVivo.UnitTests
{
    public class LoteEntregasDtoTests
    {
        private static readonly JsonSerializerOptions OpcionesJson =
            new(JsonSerializerDefaults.Web);

        [Fact]
        public void LoteEntregasRequestDto_SerializacionJson_GeneraFormatoCamelCaseCorrecto()
        {
            var salaId = Guid.NewGuid();
            var loteId = Guid.NewGuid();
            var evaluacionId = Guid.NewGuid();
            var institucionId = Guid.NewGuid();
            var alumnoId = Guid.NewGuid();
            var preguntaId = Guid.NewGuid();
            var fecha = new DateTime(2026, 11, 13, 14, 35, 0, DateTimeKind.Utc);

            var lote = new LoteEntregasRequestDto(
                Version: 1,
                LoteId: loteId,
                SalaId: salaId,
                EvaluacionId: evaluacionId,
                InstitucionId: institucionId,
                FinalizadaUtc: fecha,
                MotivoCierre: "Docente",
                Entregas: new List<EntregaAlumnoDto>
                {
                    new(
                        alumnoId,
                        new List<RespuestaEntregaDto>
                        {
                            new(preguntaId, "Respuesta A", 1, fecha)
                        },
                        new List<AlertaFocoEntregaDto>
                        {
                            new(preguntaId, "visibilitychange", 4200, "Roja", fecha)
                        }
                    )
                });

            var json = JsonSerializer.Serialize(lote, OpcionesJson);

            Assert.Contains("\"version\":1", json);
            Assert.Contains($"\"loteId\":\"{loteId}\"", json);
            Assert.Contains($"\"salaId\":\"{salaId}\"", json);
            Assert.Contains($"\"evaluacionId\":\"{evaluacionId}\"", json);
            Assert.Contains($"\"institucionId\":\"{institucionId}\"", json);
            Assert.Contains("\"motivoCierre\":\"Docente\"", json);
            Assert.Contains("\"entregas\":[", json);
            Assert.Contains($"\"alumnoId\":\"{alumnoId}\"", json);
            Assert.Contains("\"respuestas\":[", json);
            Assert.Contains("\"alertasFoco\":[", json);
            Assert.Contains("\"duracionMs\":4200", json);
            Assert.Contains("\"severidad\":\"Roja\"", json);
        }

        [Fact]
        public void LoteEntregasRequestDto_DeserializacionEjemploOficial_MapeaCamposCorrectamente()
        {
            const string jsonEjemplo = """
            {
              "version": 1,
              "loteId": "0b6f5a2e-8c1d-4e7a-9f30-2d4c6e8a1b35",
              "salaId": "6a2b7c3d-8e9f-4a01-b2c3-d4e5f6a7b8c9",
              "evaluacionId": "3c4d5e6f-7a8b-4c9d-8e0f-1a2b3c4d5e6f",
              "institucionId": "11111111-1111-1111-1111-111111111111",
              "finalizadaUtc": "2026-11-13T14:35:00Z",
              "motivoCierre": "Docente",
              "entregas": [
                {
                  "alumnoId": "11111111-0000-0000-0000-000000000003",
                  "respuestas": [
                    {
                      "preguntaId": "5f1c1d9e-0a3b-4c11-9a52-1b2c3d4e5f60",
                      "respuesta": "7a1b2c3d-4e5f-4a6b-8c7d-9e0f1a2b3c4d",
                      "secuencia": 1,
                      "recibidaUtc": "2026-11-13T14:21:10Z"
                    }
                  ],
                  "alertasFoco": [
                    {
                      "preguntaId": "9c3d4e5f-6071-4c8d-8e9f-1a2b3c4d5e6f",
                      "tipoEvento": "visibilitychange",
                      "duracionMs": 4200,
                      "severidad": "Roja",
                      "registradoUtc": "2026-11-13T14:25:03Z"
                    }
                  ]
                },
                {
                  "alumnoId": "11111111-0000-0000-0000-000000000004",
                  "respuestas": [],
                  "alertasFoco": []
                }
              ]
            }
            """;

            var lote = JsonSerializer.Deserialize<LoteEntregasRequestDto>(jsonEjemplo, OpcionesJson);

            Assert.NotNull(lote);
            Assert.Equal(1, lote.Version);
            Assert.Equal(Guid.Parse("0b6f5a2e-8c1d-4e7a-9f30-2d4c6e8a1b35"), lote.LoteId);
            Assert.Equal(Guid.Parse("6a2b7c3d-8e9f-4a01-b2c3-d4e5f6a7b8c9"), lote.SalaId);
            Assert.Equal("Docente", lote.MotivoCierre);
            Assert.Equal(2, lote.Entregas.Count);

            var alumno1 = lote.Entregas[0];
            Assert.Equal(Guid.Parse("11111111-0000-0000-0000-000000000003"), alumno1.AlumnoId);
            Assert.Single(alumno1.Respuestas);
            Assert.Equal("7a1b2c3d-4e5f-4a6b-8c7d-9e0f1a2b3c4d", alumno1.Respuestas[0].Respuesta);
            Assert.Single(alumno1.AlertasFoco);
            Assert.Equal("Roja", alumno1.AlertasFoco[0].Severidad);

            var alumno2 = lote.Entregas[1];
            Assert.Empty(alumno2.Respuestas);
            Assert.Empty(alumno2.AlertasFoco);
        }

        [Fact]
        public void ResultadoLoteEntregasDto_DeserializacionEjemploOficial_MapeaCamposCorrectamente()
        {
            const string jsonResultado = """
            {
              "loteId": "0b6f5a2e-8c1d-4e7a-9f30-2d4c6e8a1b35",
              "salaId": "6a2b7c3d-8e9f-4a01-b2c3-d4e5f6a7b8c9",
              "entregasRecibidas": 2,
              "entregasNuevas": 2,
              "entregasYaExistentes": 0,
              "entregas": [
                {
                  "alumnoId": "11111111-0000-0000-0000-000000000003",
                  "entregaId": "e1a2b3c4-d5e6-4f70-8a9b-0c1d2e3f4a5b",
                  "estado": "PendienteCorreccion",
                  "notaMultipleChoice": 1
                },
                {
                  "alumnoId": "11111111-0000-0000-0000-000000000004",
                  "entregaId": "f2b3c4d5-e6f7-4081-9bac-1d2e3f4a5b6c",
                  "estado": "Corregida",
                  "notaMultipleChoice": 0
                }
              ]
            }
            """;

            var resultado = JsonSerializer.Deserialize<ResultadoLoteEntregasDto>(jsonResultado, OpcionesJson);

            Assert.NotNull(resultado);
            Assert.Equal(Guid.Parse("0b6f5a2e-8c1d-4e7a-9f30-2d4c6e8a1b35"), resultado.LoteId);
            Assert.Equal(Guid.Parse("6a2b7c3d-8e9f-4a01-b2c3-d4e5f6a7b8c9"), resultado.SalaId);
            Assert.Equal(2, resultado.EntregasRecibidas);
            Assert.Equal(2, resultado.EntregasNuevas);
            Assert.Equal(0, resultado.EntregasYaExistentes);
            Assert.NotNull(resultado.Entregas);
            Assert.Equal(2, resultado.Entregas.Count);
            Assert.Equal("PendienteCorreccion", resultado.Entregas[0].Estado);
            Assert.Equal(1, resultado.Entregas[0].NotaMultipleChoice);
            Assert.Equal("Corregida", resultado.Entregas[1].Estado);
        }
    }
}
