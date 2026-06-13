using System.Text.Json.Serialization;

namespace CommunityHub.Models;

public class ToolCount
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("count")]
    public int Count { get; set; }
}

public class ActivitySnapshot
{
    public int SessionCount { get; set; }
    public int UserCount { get; set; }
    public int PromptSubmissions { get; set; }
    public int ToolCalls { get; set; }
    public int DistinctToolsCalled { get; set; }
    public int AgentStops { get; set; }
    public int SubagentStops { get; set; }
    public int UploadedGames { get; set; }
    public List<ToolCount> ToolBreakdown { get; set; } = [];
}

public class ActivityCountsView
{
    [JsonPropertyName("session_count")]
    public int SessionCount { get; set; }

    [JsonPropertyName("user_count")]
    public int UserCount { get; set; }

    [JsonPropertyName("prompt_submissions")]
    public int PromptSubmissions { get; set; }

    [JsonPropertyName("tool_calls")]
    public int ToolCalls { get; set; }

    [JsonPropertyName("distinct_tools_called")]
    public int DistinctToolsCalled { get; set; }

    [JsonPropertyName("agent_stops")]
    public int AgentStops { get; set; }

    [JsonPropertyName("subagent_stops")]
    public int SubagentStops { get; set; }

    [JsonPropertyName("uploaded_games_count")]
    public int UploadedGamesCount { get; set; }
}

public class ActivityScopesView
{
    [JsonPropertyName("current_tenant")]
    public ActivityCountsView CurrentTenant { get; set; } = new();

    [JsonPropertyName("all_tenants")]
    public ActivityCountsView AllTenants { get; set; } = new();
}

public class ToolBreakdownScopesView
{
    [JsonPropertyName("current_tenant")]
    public List<ToolCount> CurrentTenant { get; set; } = [];

    [JsonPropertyName("all_tenants")]
    public List<ToolCount> AllTenants { get; set; } = [];
}

public class ActivityApiView
{
    [JsonPropertyName("tenant")]
    public string Tenant { get; set; } = "";

    [JsonPropertyName("activity")]
    public ActivityScopesView Activity { get; set; } = new();

    [JsonPropertyName("tools")]
    public ToolBreakdownScopesView Tools { get; set; } = new();
}

public class GalleryEntry
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("filename")]
    public string Filename { get; set; } = "";

    [JsonPropertyName("url")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Url { get; set; }
}

public class GalleryListItem
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("url")]
    public string Url { get; set; } = "";
}

public class TenantsApiView
{
    [JsonPropertyName("current_tenant")]
    public string CurrentTenant { get; set; } = "";

    [JsonPropertyName("tenants")]
    public List<string> Tenants { get; set; } = [];
}

public class ScreenshotUploadInstructions
{
    [JsonPropertyName("upload_url")]
    public string UploadUrl { get; set; } = "";

    [JsonPropertyName("method")]
    public string Method { get; set; } = "POST";

    [JsonPropertyName("form_field")]
    public string FormField { get; set; } = "image";

    [JsonPropertyName("example_command")]
    public string ExampleCommand { get; set; } = "";

    [JsonPropertyName("instructions")]
    public string Instructions { get; set; } = "";
}

public class ShareGameInstructions
{
    [JsonPropertyName("upload_url")]
    public string UploadUrl { get; set; } = "";

    [JsonPropertyName("method")]
    public string Method { get; set; } = "POST";

    [JsonPropertyName("content_type")]
    public string ContentType { get; set; } = "text/html; charset=utf-8";

    [JsonPropertyName("example_command")]
    public string ExampleCommand { get; set; } = "";

    [JsonPropertyName("instructions")]
    public string Instructions { get; set; } = "";
}
