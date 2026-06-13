using CommunityHub.Services;

namespace CommunityHub.Endpoints;

public static class EventEndpoints
{
    public static WebApplication MapEventEndpoints(this WebApplication app)
    {
        app.MapPost("/api/event/session_start", async (HttpContext ctx, IMetricsStore metrics) =>
        {
            var sessionId = ctx.Request.Query["session_id"].ToString();
            var userInfo = ctx.Request.Query["user_info"].ToString();
            try { await metrics.OnSessionStartAsync(sessionId, userInfo); }
            catch (Exception ex) { Console.WriteLine($"session_start error: {ex.Message}"); }
            return Results.Ok();
        });

        app.MapPost("/api/event/user_prompt_submitted", async (IMetricsStore metrics) =>
        {
            try { await metrics.OnPromptSubmittedAsync(); }
            catch (Exception ex) { Console.WriteLine($"user_prompt_submitted error: {ex.Message}"); }
            return Results.Ok();
        });

        app.MapPost("/api/event/tool_used", async (HttpContext ctx, IMetricsStore metrics) =>
        {
            var toolName = ctx.Request.Query["tool_name"].ToString();
            if (string.IsNullOrWhiteSpace(toolName))
                return Results.BadRequest("tool_name is required");

            try { await metrics.OnToolUsedAsync(toolName); }
            catch (Exception ex) { Console.WriteLine($"tool_used error: {ex.Message}"); }
            return Results.Ok();
        });

        app.MapPost("/api/event/agent_stop", async (IMetricsStore metrics) =>
        {
            try { await metrics.OnAgentStopAsync(); }
            catch (Exception ex) { Console.WriteLine($"agent_stop error: {ex.Message}"); }
            return Results.Ok();
        });

        app.MapPost("/api/event/subagent_stop", async (IMetricsStore metrics) =>
        {
            try { await metrics.OnSubagentStopAsync(); }
            catch (Exception ex) { Console.WriteLine($"subagent_stop error: {ex.Message}"); }
            return Results.Ok();
        });

        return app;
    }
}
