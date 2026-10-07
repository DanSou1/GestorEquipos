using System.Diagnostics;
using Gestor_Equipos.Services.Diagnostics;
using Microsoft.AspNetCore.Mvc.Controllers;

namespace Gestor_Equipos.Middleware;

public sealed class DatabaseAvailabilityMiddleware(RequestDelegate next, ILogger<DatabaseAvailabilityMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var timer = Stopwatch.StartNew();
        int? failureStatus = null;
        using var scope = logger.BeginScope(new Dictionary<string, object> { ["RequestId"] = context.TraceIdentifier });
        try
        {
            await next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // The client has disconnected; do not retry or write an error page.
            if (!context.Response.HasStarted)
                context.Response.StatusCode = 499;
        }
        catch (Exception exception) when (!context.Response.HasStarted && DatabaseAvailability.IsUnavailable(exception))
        {
            // Do not log SQL text, parameters, credentials or the exception message.
            logger.LogWarning("Database unavailable. Request {RequestId}", context.TraceIdentifier);
            context.Response.Clear();
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            context.Response.ContentType = "text/html; charset=utf-8";
            context.Response.Headers.CacheControl = "no-store";
            context.Response.Headers.RetryAfter = "10";
            // No redirect, authentication, Razor layout or database dependency here.
            // In particular, do not sign out a valid cookie when SQL cannot validate it.
            await context.Response.WriteAsync("""
                <!doctype html><html lang="es"><meta charset="utf-8">
                <meta name="viewport" content="width=device-width, initial-scale=1">
                <title>Servicio temporalmente no disponible</title>
                <main><h1>Servicio temporalmente no disponible</h1>
                <p>No pudimos completar la solicitud. Intenta nuevamente en unos momentos.</p>
                <p>Si estabas guardando cambios, verifica el resultado antes de repetir la operación.</p>
                <a href="/">Volver al inicio</a></main></html>
                """, context.RequestAborted);
        }
        catch
        {
            failureStatus = StatusCodes.Status500InternalServerError;
            throw;
        }
        finally
        {
            var action = context.GetEndpoint()?.Metadata.GetMetadata<ControllerActionDescriptor>();
            var route = action is null ? "unmatched" : $"{action.ControllerName}/{action.ActionName}";
            logger.LogInformation("Request {RequestId} {Method} {Route} returned {StatusCode} in {ElapsedMs} ms",
                context.TraceIdentifier, context.Request.Method, route,
                failureStatus ?? context.Response.StatusCode, timer.ElapsedMilliseconds);
        }
    }

}
