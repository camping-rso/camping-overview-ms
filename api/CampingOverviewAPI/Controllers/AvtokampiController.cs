﻿using CampingOverviewAPI.Models;
using CampingOverviewAPI.Services.Interfaces;
using GraphQL;
using GraphQL.Client.Http;
using GraphQL.Client.Serializer.Newtonsoft;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CampingOverviewAPI.Controllers
{
    [Produces("application/json")]
    [Route("api/[controller]")]
    [ApiController]
    public class AvtokampiController : ControllerBase
    {
        private readonly IAvtokampiRepository _avtokampiService;
        private readonly ILogger _logger;
        // To use NewtonsoftJsonSerializer, add a reference to NuGet package GraphQL.Client.Serializer.Newtonsoft

        public AvtokampiController(IAvtokampiRepository avtokampiService, ILogger<AvtokampiController> logger)
        {
            _avtokampiService = avtokampiService;
            _logger = logger;
        }


        /// <summary>
        ///     Seznam avtokampov na stran
        /// </summary>
        /// <remarks>
        /// Primer zahtevka:
        ///
        ///     GET api/avtokampi/paging
        ///
        /// </remarks>
        /// <returns>Seznam aktivnih avtokampov</returns>
        /// <response code="200">Seznam avtokampov</response>
        /// <response code="400">Bad request error message</response>
        /// <response code="404">Not found error message</response>
        [HttpGet("Paging")]
        [ProducesResponseType(200)]
        [ProducesResponseType(404)]
        [ProducesResponseType(400)]
        public async Task<IActionResult> GetAvtokampiPaging([FromQuery] AvtokampiParameters avtokampiParameters)
        {
            try
            {
                var result = await _avtokampiService.GetPage(avtokampiParameters);
                if (result == null)
                {
                    return NotFound(/*new ErrorHandlerModel($"Avtokamp z ID { id }, ne obstaja.", HttpStatusCode.NotFound)*/);
                }

                var metadata = new
                {
                    result.TotalCount,
                    result.PageSize,
                    result.CurrentPage,
                    result.TotalPages,
                    result.HasNext,
                    result.HasPrevious
                };

                Response.Headers.Add("X-Pagination", JsonConvert.SerializeObject(metadata));

                _logger.LogInformation($"Returned {result.TotalCount} owners from database.");

                return Ok(result);
            }
            catch (Exception e)
            {
                _logger.LogError("GET all avtokampi Unhandled exception ...", e);
                return BadRequest(/*new ErrorHandlerModel(e.Message, HttpStatusCode.BadRequest)*/);
            }
        }

        /// <summary>
        ///     Seznam avtokampov
        /// </summary>
        /// <remarks>
        /// Primer zahtevka:
        ///
        ///     GET api/avtokampi
        ///
        /// </remarks>
        /// <returns>Seznam vseh aktivnih avtokampov</returns>
        /// <response code="200">Seznam avtokampov</response>
        /// <response code="400">Bad request error message</response>
        /// <response code="404">Not found error message</response>
        [HttpGet]
        [ProducesResponseType(200)]
        [ProducesResponseType(404)]
        [ProducesResponseType(400)]
        public async Task<IActionResult> GetAllAvtokampi()
        {
            try
            {
                var graphQLClient = new GraphQLHttpClient(Environment.GetEnvironmentVariable("GRAPHQL_ENDPOINT"), new NewtonsoftJsonSerializer());

                var logRequest = new GraphQLRequest
                {
                    Query = @"
                        mutation addLog {
                          createLog(microservice: 'camping-overview-ms', message: 'Retrieving all camps...') {
                            log { microservice 
                                  message 
                            }
                          }
                        }"
                };
                await graphQLClient.SendMutationAsync<MutationResult>(logRequest);

                var result = await _avtokampiService.GetAll();

                logRequest = new GraphQLRequest
                {
                    Query = @"
                        mutation addLog {
                          createLog(microservice: 'camping-overview-ms', message: 'All camps were retrieved...') {
                            log { microservice 
                                  message 
                            }
                          }
                        }"
                };
                await graphQLClient.SendMutationAsync<MutationResult>(logRequest);

                if (result == null)
                {
                    return NotFound(/*new ErrorHandlerModel($"Avtokamp z ID { id }, ne obstaja.", HttpStatusCode.NotFound)*/);
                }
                return Ok(result);
            }
            catch (Exception e)
            {
                _logger.LogError("GET all avtokampi Unhandled exception ...", e);
                return BadRequest(/*new ErrorHandlerModel(e.Message, HttpStatusCode.BadRequest)*/);
            }
        }

        /// <summary>
        ///     Podatki o posameznemu avtokampu
        /// </summary>
        /// <remarks>
        /// Primer zahtevka:
        ///
        ///     GET api/avtokampi/1234
        ///
        /// </remarks>
        /// <returns>Objekt Avtokamp</returns>
        /// <param name="kamp_id">Identifikator avtokampa</param>
        /// <response code="200">Avtokamp</response>
        /// <response code="400">Bad request error message</response>
        /// <response code="404">Not found error message</response>
        [HttpGet("{kamp_id}")]
        [ProducesResponseType(200)]
        [ProducesResponseType(404)]
        [ProducesResponseType(400)]
        public async Task<IActionResult> GetAvtokamp(int kamp_id)
        {
            try
            {
                var result = await _avtokampiService.GetAvtokampByID(kamp_id);
                if (result == null)
                {
                    return NotFound(/*new ErrorHandlerModel($"Avtokamp z ID { id }, ne obstaja.", HttpStatusCode.NotFound)*/);
                }
                return Ok(result);
            }
            catch (ArgumentException)
            {
                return BadRequest(/*new ErrorHandlerModel($"Argument ID { id } ni v pravilni obliki.", HttpStatusCode.BadRequest)*/);
            }
            catch (Exception e)
            {
                _logger.LogError("GET avtokamp Unhandled exception ...", e);
                return BadRequest(/*new ErrorHandlerModel(e.Message, HttpStatusCode.BadRequest)*/);
            }
        }

        /// <summary>
        ///     Dodajanje novega avtokampa
        /// </summary>
        /// <remarks>
        /// Primer zahtevka:
        ///
        ///     POST api/avtokampi
        ///     {
        ///         "naziv": "Kamp Adria",
        ///         "opis": "Kamp Adria je relativno nov kamp, ki se nahaja v središču Ankarana.",
        ///         "telefon": "032234434",
        ///         "naslov": "Jadranska cesta 25, 6280 Ankaran",
        ///         "naziv_lokacije": "Ankaran",
        ///         "koordinata_x": "14.200453",
        ///         "koordinata_y": "77.200864",
        ///         "regija": 4
        ///     }
        ///
        /// </remarks>
        /// <returns>Boolean value, success or not</returns>
        /// <param name="avtokamp">Podatki novega avtokampa</param>
        /// <response code="201">If successfully created: true or false</response>
        /// <response code="400">Bad request error message</response>
        /// <response code="404">Not found error message</response>
        [HttpPost]
        [ProducesResponseType(201)]
        [ProducesResponseType(404)]
        [ProducesResponseType(400)]
        public async Task<IActionResult> CreateAvtokamp([FromBody] Avtokampi avtokamp)
        {
            try
            {
                var result = await _avtokampiService.CreateAvtokamp(avtokamp);
                if (result == false)
                {
                    return NotFound(/*new ErrorHandlerModel($"Avtokamp z ID { id }, ne obstaja.", HttpStatusCode.NotFound)*/);
                }
                return Created("/avtokampi/id", result);
            }
            catch (ArgumentException)
            {
                return BadRequest(/*new ErrorHandlerModel($"Argument ID { id } ni v pravilni obliki.", HttpStatusCode.BadRequest)*/);
            }
            catch (Exception e)
            {
                _logger.LogError("CREATE avtokamp Unhandled exception ...", e);
                return BadRequest(/*new ErrorHandlerModel(e.Message, HttpStatusCode.BadRequest)*/);
            }
        }

        /// <summary>
        ///     Urejanje podatkov o avtokampu
        /// </summary>
        /// <remarks>
        /// Primer zahtevka:
        ///
        ///     PUT api/avtokampi/1234
        ///     {
        ///         "avtokamp_id": 1,
        ///         "naziv": "Kamp Njivice 2",
        ///         "opis": "Kamp Njivice 2 ima idilično lego ob morju.",
        ///         "naslov": "Večna pot 112, 1000 Ljubljana",
        ///         "telefon": "083211232",
        ///         "naziv_lokacije": "Njivice",
        ///         "koordinata_x": "45.33399",
        ///         "koordinata_y": "22.19993",
        ///         "regija": 1
        ///     }
        ///
        /// </remarks>
        /// <returns>Objekt Avtokamp</returns>
        /// <param name="avtokamp">Podatki popravljenega avtokampa</param>
        /// <param name="kamp_id">Identifikator avtokampa</param>
        /// <response code="204">No content</response>
        /// <response code="400">Bad request error message</response>
        /// <response code="404">Not found error message</response>
        [HttpPut("{kamp_id}")]
        [ProducesResponseType(204)]
        [ProducesResponseType(404)]
        [ProducesResponseType(400)]
        public async Task<IActionResult> UpdateAvtokampi([FromBody] Avtokampi avtokamp, int kamp_id)
        {
            try
            {
                var result = await _avtokampiService.UpdateAvtokamp(avtokamp, kamp_id);
                if (result == null)
                {
                    return NotFound(/*new ErrorHandlerModel($"Avtokamp z ID { id }, ne obstaja.", HttpStatusCode.NotFound)*/);
                }
                return NoContent();
            }
            catch (ArgumentException)
            {
                return BadRequest(/*new ErrorHandlerModel($"Argument ID { id } ni v pravilni obliki.", HttpStatusCode.BadRequest)*/);
            }
            catch (Exception e)
            {
                _logger.LogError("UPDATE avtokamp Unhandled exception ...", e);
                return BadRequest(/*new ErrorHandlerModel(e.Message, HttpStatusCode.BadRequest)*/);
            }
        }

        /// <summary>
        ///     Brisanje avtokampa
        /// </summary>
        /// <remarks>
        /// Primer zahtevka:
        ///
        ///     DELETE api/avtokampi/1234
        ///
        /// </remarks>
        /// <returns>Boolean value</returns>
        /// <param name="kamp_id">Identifikator avtokampa</param>
        /// <response code="204">No content</response>
        /// <response code="400">Bad request error message</response>
        /// <response code="404">Not found error message</response>
        [HttpDelete("{kamp_id}")]
        [ProducesResponseType(204)]
        [ProducesResponseType(404)]
        [ProducesResponseType(400)]
        public async Task<IActionResult> DeleteAvtokampi(int kamp_id)
        {
            try
            {
                var result = await _avtokampiService.RemoveAvtokamp(kamp_id);
                if (result == false)
                {
                    return NotFound(/*new ErrorHandlerModel($"Avtokamp z ID { id }, ne obstaja.", HttpStatusCode.NotFound)*/);
                }
                return NoContent();
            }
            catch (ArgumentException)
            {
                return BadRequest(/*new ErrorHandlerModel($"Argument ID { id } ni v pravilni obliki.", HttpStatusCode.BadRequest)*/);
            }
            catch (Exception e)
            {
                _logger.LogError("DELETE avtokamp Unhandled exception ...", e);
                return BadRequest(/*new ErrorHandlerModel(e.Message, HttpStatusCode.BadRequest)*/);
            }
        }

        /// <summary>
        ///     Ceniki avtokampa
        /// </summary>
        /// <remarks>
        /// Primer zahtevka:
        ///
        ///     GET api/avtokampi/1234
        ///
        /// </remarks>
        /// <returns>Objekt Avtokamp</returns>
        /// <param name="kamp_id">Identifikator avtokampa</param>
        /// <response code="200">Avtokamp</response>
        /// <response code="400">Bad request error message</response>
        /// <response code="404">Not found error message</response>
        [HttpGet("{kamp_id}/ceniki")]
        [ProducesResponseType(200)]
        [ProducesResponseType(404)]
        [ProducesResponseType(400)]
        public async Task<IActionResult> GetCenikiAvtokampa(int kamp_id)
        {
            try
            {
                var result = await _avtokampiService.GetCenikiAvtokampa(kamp_id);
                if (result == null)
                {
                    return NotFound(/*new ErrorHandlerModel($"Cenik z ID { id }, ne obstaja.", HttpStatusCode.NotFound)*/);
                }
                return Ok(result);
            }
            catch (ArgumentException)
            {
                return BadRequest(/*new ErrorHandlerModel($"Argument ID { id } ni v pravilni obliki.", HttpStatusCode.BadRequest)*/);
            }
            catch (Exception e)
            {
                _logger.LogError("GET GetCenikiAvtokampa Unhandled exception ...", e);
                return BadRequest(/*new ErrorHandlerModel(e.Message, HttpStatusCode.BadRequest)*/);
            }
        }

        /// <summary>
        ///     Podrobnosti cenika avtokampa
        /// </summary>
        /// <remarks>
        /// Primer zahtevka:
        ///
        ///     GET api/avtokampi/1234
        ///
        /// </remarks>
        /// <returns>Objekt Avtokamp</returns>
        /// <param name="cenik_id">Identifikator avtokampa</param>
        /// <response code="200">Avtokamp</response>
        /// <response code="400">Bad request error message</response>
        /// <response code="404">Not found error message</response>
        [HttpGet("{cenik_id}/cenik")]
        [ProducesResponseType(200)]
        [ProducesResponseType(404)]
        [ProducesResponseType(400)]
        public async Task<IActionResult> GetCenikAvtokampa(int cenik_id)
        {
            try
            {
                var result = await _avtokampiService.GetCenikAvtokampa(cenik_id);
                if (result == null)
                {
                    return NotFound(/*new ErrorHandlerModel($"Cenik z ID { id }, ne obstaja.", HttpStatusCode.NotFound)*/);
                }
                return Ok(result);
            }
            catch (ArgumentException)
            {
                return BadRequest(/*new ErrorHandlerModel($"Argument ID { id } ni v pravilni obliki.", HttpStatusCode.BadRequest)*/);
            }
            catch (Exception e)
            {
                _logger.LogError("GET GetCenikiAvtokampa Unhandled exception ...", e);
                return BadRequest(/*new ErrorHandlerModel(e.Message, HttpStatusCode.BadRequest)*/);
            }
        }

        /// <summary>
        ///     Dodajanje novega cenika avtokampa
        /// </summary>
        /// <remarks>
        /// Primer zahtevka:
        ///
        ///     POST api/avtokampi
        ///     {
        ///         "naziv": "Ime avtokampa",
        ///         "cena": 180
        ///     }
        ///
        /// </remarks>
        /// <returns>Boolean value, success or not</returns>
        /// <param name="cenik">Podatki novega avtokampa</param>
        /// <param name="kamp_id"></param>
        /// <response code="201">If successfully created: true or false</response>
        /// <response code="400">Bad request error message</response>
        /// <response code="404">Not found error message</response>
        [HttpPost("{kamp_id}/cenik")]
        [ProducesResponseType(201)]
        [ProducesResponseType(404)]
        [ProducesResponseType(400)]
        public async Task<IActionResult> CreateCenikAvtokampa([FromBody] Ceniki cenik, int kamp_id)
        {
            try
            {
                var result = await _avtokampiService.CreateCenikAvtokampa(cenik, kamp_id);
                if (result == false)
                {
                    return NotFound(/*new ErrorHandlerModel($"Cenik z ID { id }, ne obstaja.", HttpStatusCode.NotFound)*/);
                }
                return Created("/avtokampi/id", result);
            }
            catch (ArgumentException)
            {
                return BadRequest(/*new ErrorHandlerModel($"Argument ID { id } ni v pravilni obliki.", HttpStatusCode.BadRequest)*/);
            }
            catch (Exception e)
            {
                _logger.LogError("CREATE CreateCenikAvtokampa Unhandled exception ...", e);
                return BadRequest(/*new ErrorHandlerModel(e.Message, HttpStatusCode.BadRequest)*/);
            }
        }

        /// <summary>
        ///     Urejanje podatkov o ceniku
        /// </summary>
        /// <remarks>
        /// Primer zahtevka:
        ///
        ///     PUT api/avtokampi/1234
        ///     {
        ///         "naziv": "Novo ime avtokampa"
        ///     }
        ///
        /// </remarks>
        /// <returns>Objekt Avtokamp</returns>
        /// <param name="cenik">Podatki popravljenega avtokampa</param>
        /// <param name="cenik_id">Identifikator avtokampa</param>
        /// <response code="204">No content</response>
        /// <response code="400">Bad request error message</response>
        /// <response code="404">Not found error message</response>
        [HttpPut("{cenik_id}/cenik")]
        [ProducesResponseType(204)]
        [ProducesResponseType(404)]
        [ProducesResponseType(400)]
        public async Task<IActionResult> UpdateCenikAvtokampa([FromBody] Ceniki cenik, int cenik_id)
        {
            try
            {
                var result = await _avtokampiService.UpdateCenik(cenik, cenik_id);
                if (result == null)
                {
                    return NotFound(/*new ErrorHandlerModel($"Cenik z ID { id }, ne obstaja.", HttpStatusCode.NotFound)*/);
                }
                return NoContent();
            }
            catch (ArgumentException)
            {
                return BadRequest(/*new ErrorHandlerModel($"Argument ID { id } ni v pravilni obliki.", HttpStatusCode.BadRequest)*/);
            }
            catch (Exception e)
            {
                _logger.LogError("UPDATE UpdateCenikAvtokampa Unhandled exception ...", e);
                return BadRequest(/*new ErrorHandlerModel(e.Message, HttpStatusCode.BadRequest)*/);
            }
        }

        /// <summary>
        ///     Brisanje cenika
        /// </summary>
        /// <remarks>
        /// Primer zahtevka:
        ///
        ///     DELETE api/avtokampi/1234/cenik
        ///
        /// </remarks>
        /// <returns>Boolean value</returns>
        /// <param name="cenik_id">Identifikator avtokampa</param>
        /// <response code="204">No content</response>
        /// <response code="400">Bad request error message</response>
        /// <response code="404">Not found error message</response>
        [HttpDelete("{cenik_id}/cenik")]
        [ProducesResponseType(204)]
        [ProducesResponseType(404)]
        [ProducesResponseType(400)]
        public async Task<IActionResult> DeleteCenikAvtokampa(int cenik_id)
        {
            try
            {
                var result = await _avtokampiService.RemoveCenikAvtokampa(cenik_id);
                if (result == false)
                {
                    return NotFound(/*new ErrorHandlerModel($"Cenik z ID { id }, ne obstaja.", HttpStatusCode.NotFound)*/);
                }
                return NoContent();
            }
            catch (ArgumentException)
            {
                return BadRequest(/*new ErrorHandlerModel($"Argument ID { id } ni v pravilni obliki.", HttpStatusCode.BadRequest)*/);
            }
            catch (Exception e)
            {
                _logger.LogError("DELETE avtokamp Unhandled exception ...", e);
                return BadRequest(/*new ErrorHandlerModel(e.Message, HttpStatusCode.BadRequest)*/);
            }
        }

        /// <summary>
        ///     Seznam regij
        /// </summary>
        /// <remarks>
        /// Primer zahtevka:
        ///
        ///     GET api/avtokampi
        ///
        /// </remarks>
        /// <returns>Seznam vseh aktivnih avtokampov</returns>
        /// <response code="200">Seznam avtokampov</response>
        /// <response code="400">Bad request error message</response>
        /// <response code="404">Not found error message</response>
        [HttpGet("regije")]
        [ProducesResponseType(200)]
        [ProducesResponseType(404)]
        [ProducesResponseType(400)]
        public async Task<IActionResult> GetRegije()
        {
            try
            {
                var result = await _avtokampiService.GetRegije();
                if (result == null)
                {
                    return NotFound(/*new ErrorHandlerModel($"Regija z ID { id }, ne obstaja.", HttpStatusCode.NotFound)*/);
                }
                return Ok(result);
            }
            catch (Exception e)
            {
                _logger.LogError("GET GetRegije Unhandled exception ...", e);
                return BadRequest(/*new ErrorHandlerModel(e.Message, HttpStatusCode.BadRequest)*/);
            }
        }

        /// <summary>
        ///     Seznam držav
        /// </summary>
        /// <remarks>
        /// Primer zahtevka:
        ///
        ///     GET api/avtokampi
        ///
        /// </remarks>
        /// <returns>Seznam vseh aktivnih avtokampov</returns>
        /// <response code="200">Seznam avtokampov</response>
        /// <response code="400">Bad request error message</response>
        /// <response code="404">Not found error message</response>
        [HttpGet("drzave")]
        [ProducesResponseType(200)]
        [ProducesResponseType(404)]
        [ProducesResponseType(400)]
        public async Task<IActionResult> GetDrzave()
        {
            try
            {
                var result = await _avtokampiService.GetDrzave();
                if (result == null)
                {
                    return NotFound(/*new ErrorHandlerModel($"Država z ID { id }, ne obstaja.", HttpStatusCode.NotFound)*/);
                }
                return Ok(result);
            }
            catch (Exception e)
            {
                _logger.LogError("GET GetDrzave Unhandled exception ...", e);
                return BadRequest(/*new ErrorHandlerModel(e.Message, HttpStatusCode.BadRequest)*/);
            }
        }
    }
}

public class MutationResult
{
    public Log log { get; set; }

    public class Log
    {
        public string microservice { get; set; }

        public string message { get; set; }
    }
}