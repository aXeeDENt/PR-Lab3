using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace MemoryScramble
{
    /// <summary>
    /// Web server for Memory Scramble game.
    /// Provides HTTP API endpoints that delegate to Commands module.
    /// </summary>
    public class WebServer
    {
        private readonly Board _board;
        private readonly int _requestedPort;
        public int Port => _requestedPort;

        /// <summary>
        /// Creates a new WebServer instance.
        /// 
        /// Requires: board is non-null; requestedPort > 0
        /// </summary>
        public WebServer(Board board, int requestedPort)
        {
            _board = board ?? throw new ArgumentNullException(nameof(board));
            _requestedPort = requestedPort;
        }

        /// <summary>
        /// Starts the web server asynchronously.
        /// 
        /// Effects: Listens on configured port; serves static files and game endpoints
        /// </summary>
        public async Task StartAsync()
        {
            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseUrls($"http://localhost:{_requestedPort}");

            var app = builder.Build();

            // Serve static files from Public folder
            app.UseStaticFiles();

            // GET /look/{playerId}
            app.MapGet("/look/{playerId}", (string playerId, HttpContext context) =>
            {
                try
                {
                    var result = Commands.Look(_board, playerId);
                    context.Response.ContentType = "text/plain";
                    return Results.Ok(result);
                }
                catch (Exception ex)
                {
                    context.Response.StatusCode = 400;
                    return Results.BadRequest($"Error: {ex.Message}");
                }
            });

            // GET /flip/{playerId}/{row}/{column}
            app.MapGet("/flip/{playerId}/{row}/{column}", (string playerId, int row, int column, HttpContext context) =>
            {
                try
                {
                    var result = Commands.Flip(_board, playerId, row, column);
                    context.Response.ContentType = "text/plain";
                    return Results.Ok(result);
                }
                catch (Exception ex)
                {
                    context.Response.StatusCode = 400;
                    return Results.BadRequest($"Error: {ex.Message}");
                }
            });

            // GET /map/{playerId}/{fromCard}/{toCard}
            app.MapGet("/map/{playerId}/{fromCard}/{toCard}", (string playerId, string fromCard, string toCard, HttpContext context) =>
            {
                try
                {
                    var result = Commands.Map(_board, playerId, card => 
                        card == fromCard ? toCard : card);
                    context.Response.ContentType = "text/plain";
                    return Results.Ok(result);
                }
                catch (Exception ex)
                {
                    context.Response.StatusCode = 400;
                    return Results.BadRequest($"Error: {ex.Message}");
                }
            });

            // GET /watch/{playerId}
            app.MapGet("/watch/{playerId}", async (string playerId, HttpContext context) =>
            {
                try
                {
                    var result = await Commands.Watch(_board, playerId);
                    context.Response.ContentType = "text/plain";
                    return Results.Ok(result);
                }
                catch (Exception ex)
                {
                    context.Response.StatusCode = 400;
                    return Results.BadRequest($"Error: {ex.Message}");
                }
            });

            Console.WriteLine($"Server listening at http://localhost:{_requestedPort}");
            await app.RunAsync();
        }
    }
}