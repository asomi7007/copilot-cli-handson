using System.Collections.Concurrent;

namespace CommunityHub.Services;

public static class OpenApiSpecBuilder
{
    private static readonly ConcurrentDictionary<string, object> Cache = new();

    public static object Build(string serverUrl) =>
        Cache.GetOrAdd(serverUrl, BuildSpec);

    private static object BuildSpec(string serverUrl) => new Dictionary<string, object>
    {
        ["openapi"] = "3.1.0",
        ["info"] = new Dictionary<string, object>
        {
            ["title"] = "Lab 502 Community Hub API",
            ["version"] = "1.0.0",
            ["description"] = "Machine-readable API description for the Lab 502 Community Hub /api endpoints."
        },
        ["servers"] = new[] { new Dictionary<string, object> { ["url"] = serverUrl } },
        ["paths"] = new Dictionary<string, object>
        {
            ["/api/openapi.json"] = new Dictionary<string, object>
            {
                ["get"] = new Dictionary<string, object>
                {
                    ["summary"] = "Get OpenAPI document",
                    ["operationId"] = "getOpenAPI",
                    ["responses"] = new Dictionary<string, object>
                    {
                        ["200"] = new Dictionary<string, object>
                        {
                            ["description"] = "OpenAPI JSON document",
                            ["content"] = new Dictionary<string, object> { ["application/json"] = new Dictionary<string, object>() }
                        }
                    }
                }
            },
            ["/api/event/session_start"] = new Dictionary<string, object>
            {
                ["post"] = new Dictionary<string, object>
                {
                    ["summary"] = "Record session start",
                    ["operationId"] = "recordSessionStart",
                    ["parameters"] = new object[]
                    {
                        new Dictionary<string, object> { ["name"] = "session_id", ["in"] = "query", ["required"] = false, ["schema"] = new Dictionary<string, object> { ["type"] = "string" } },
                        new Dictionary<string, object> { ["name"] = "user_info", ["in"] = "query", ["required"] = false, ["schema"] = new Dictionary<string, object> { ["type"] = "string" } }
                    },
                    ["responses"] = new Dictionary<string, object>
                    {
                        ["200"] = new Dictionary<string, object> { ["description"] = "Activity recorded" }
                    }
                }
            },
            ["/api/event/user_prompt_submitted"] = new Dictionary<string, object>
            {
                ["post"] = new Dictionary<string, object>
                {
                    ["summary"] = "Record prompt submission",
                    ["operationId"] = "recordPromptSubmission",
                    ["parameters"] = new object[]
                    {
                        new Dictionary<string, object> { ["name"] = "session_id", ["in"] = "query", ["required"] = false, ["schema"] = new Dictionary<string, object> { ["type"] = "string" } }
                    },
                    ["responses"] = new Dictionary<string, object>
                    {
                        ["200"] = new Dictionary<string, object> { ["description"] = "Event accepted" }
                    }
                }
            },
            ["/api/event/tool_used"] = new Dictionary<string, object>
            {
                ["post"] = new Dictionary<string, object>
                {
                    ["summary"] = "Record tool usage",
                    ["operationId"] = "recordToolUsage",
                    ["parameters"] = new object[]
                    {
                        new Dictionary<string, object> { ["name"] = "session_id", ["in"] = "query", ["required"] = false, ["schema"] = new Dictionary<string, object> { ["type"] = "string" } },
                        new Dictionary<string, object> { ["name"] = "tool_name", ["in"] = "query", ["required"] = true, ["schema"] = new Dictionary<string, object> { ["type"] = "string" } }
                    },
                    ["responses"] = new Dictionary<string, object>
                    {
                        ["200"] = new Dictionary<string, object> { ["description"] = "Activity recorded" }
                    }
                }
            },
            ["/api/event/subagent_stop"] = new Dictionary<string, object>
            {
                ["post"] = new Dictionary<string, object>
                {
                    ["summary"] = "Record subagent stop",
                    ["operationId"] = "recordSubagentStop",
                    ["parameters"] = new object[]
                    {
                        new Dictionary<string, object> { ["name"] = "session_id", ["in"] = "query", ["required"] = false, ["schema"] = new Dictionary<string, object> { ["type"] = "string" } }
                    },
                    ["responses"] = new Dictionary<string, object>
                    {
                        ["200"] = new Dictionary<string, object> { ["description"] = "Activity recorded" }
                    }
                }
            },
            ["/api/event/agent_stop"] = new Dictionary<string, object>
            {
                ["post"] = new Dictionary<string, object>
                {
                    ["summary"] = "Record agent stop",
                    ["operationId"] = "recordAgentStop",
                    ["parameters"] = new object[]
                    {
                        new Dictionary<string, object> { ["name"] = "session_id", ["in"] = "query", ["required"] = false, ["schema"] = new Dictionary<string, object> { ["type"] = "string" } }
                    },
                    ["responses"] = new Dictionary<string, object>
                    {
                        ["200"] = new Dictionary<string, object> { ["description"] = "Activity recorded" }
                    }
                }
            },
            ["/api/image"] = new Dictionary<string, object>
            {
                ["post"] = new Dictionary<string, object>
                {
                    ["summary"] = "Upload screenshot image",
                    ["operationId"] = "uploadImage",
                    ["parameters"] = new object[]
                    {
                        TenantQueryParameter()
                    },
                    ["requestBody"] = new Dictionary<string, object>
                    {
                        ["required"] = true,
                        ["content"] = new Dictionary<string, object>
                        {
                            ["multipart/form-data"] = new Dictionary<string, object>
                            {
                                ["schema"] = new Dictionary<string, object>
                                {
                                    ["type"] = "object",
                                    ["properties"] = new Dictionary<string, object>
                                    {
                                        ["image"] = new Dictionary<string, object> { ["type"] = "string", ["format"] = "binary" }
                                    },
                                    ["required"] = new[] { "image" }
                                }
                            }
                        }
                    },
                    ["responses"] = new Dictionary<string, object>
                    {
                        ["200"] = new Dictionary<string, object>
                        {
                            ["description"] = "Image saved",
                            ["content"] = new Dictionary<string, object> { ["text/plain"] = new Dictionary<string, object>() }
                        }
                    }
                }
            },
            ["/api/screenshots"] = new Dictionary<string, object>
            {
                ["get"] = new Dictionary<string, object>
                {
                    ["summary"] = "List screenshot filenames",
                    ["operationId"] = "listScreenshots",
                    ["parameters"] = new object[]
                    {
                        TenantQueryParameter()
                    },
                    ["responses"] = new Dictionary<string, object>
                    {
                        ["200"] = new Dictionary<string, object>
                        {
                            ["description"] = "Screenshot filenames",
                            ["content"] = new Dictionary<string, object>
                            {
                                ["application/json"] = new Dictionary<string, object>
                                {
                                    ["schema"] = new Dictionary<string, object>
                                    {
                                        ["type"] = "array",
                                        ["items"] = new Dictionary<string, object> { ["type"] = "string" }
                                    }
                                }
                            }
                        }
                    }
                }
            },
            ["/api/screenshots/{name}"] = new Dictionary<string, object>
            {
                ["get"] = new Dictionary<string, object>
                {
                    ["summary"] = "Fetch screenshot file",
                    ["operationId"] = "getScreenshot",
                    ["parameters"] = new object[]
                    {
                        new Dictionary<string, object> { ["name"] = "name", ["in"] = "path", ["required"] = true, ["schema"] = new Dictionary<string, object> { ["type"] = "string" } }
                    },
                    ["responses"] = new Dictionary<string, object>
                    {
                        ["200"] = new Dictionary<string, object>
                        {
                            ["description"] = "Screenshot binary",
                            ["content"] = new Dictionary<string, object>
                            {
                                ["image/*"] = new Dictionary<string, object>(),
                                ["application/octet-stream"] = new Dictionary<string, object>()
                            }
                        },
                        ["404"] = new Dictionary<string, object> { ["description"] = "Screenshot not found" }
                    }
                }
            },
            ["/api/invaders-gallery"] = new Dictionary<string, object>
            {
                ["post"] = new Dictionary<string, object>
                {
                    ["summary"] = "Upload Space Invaders HTML",
                    ["operationId"] = "uploadInvadersHTML",
                    ["parameters"] = new object[]
                    {
                        new Dictionary<string, object> { ["name"] = "name", ["in"] = "query", ["required"] = true, ["schema"] = new Dictionary<string, object> { ["type"] = "string" } },
                        TenantQueryParameter()
                    },
                    ["requestBody"] = new Dictionary<string, object>
                    {
                        ["required"] = true,
                        ["content"] = new Dictionary<string, object>
                        {
                            ["text/html"] = new Dictionary<string, object>
                            {
                                ["schema"] = new Dictionary<string, object> { ["type"] = "string", ["maxLength"] = 204800 }
                            },
                            ["text/plain"] = new Dictionary<string, object>
                            {
                                ["schema"] = new Dictionary<string, object> { ["type"] = "string", ["maxLength"] = 204800 }
                            }
                        }
                    },
                    ["responses"] = new Dictionary<string, object>
                    {
                        ["200"] = new Dictionary<string, object>
                        {
                            ["description"] = "Gallery entry created",
                            ["content"] = new Dictionary<string, object>
                            {
                                ["application/json"] = new Dictionary<string, object>
                                {
                                    ["schema"] = new Dictionary<string, object> { ["$ref"] = "#/components/schemas/GalleryListItem" }
                                }
                            }
                        },
                        ["400"] = new Dictionary<string, object> { ["description"] = "Invalid request" }
                    }
                }
            },
            ["/api/invaders-gallery/list"] = new Dictionary<string, object>
            {
                ["get"] = new Dictionary<string, object>
                {
                    ["summary"] = "List gallery entries",
                    ["operationId"] = "listInvadersGallery",
                    ["parameters"] = new object[]
                    {
                        TenantQueryParameter(),
                        new Dictionary<string, object> { ["name"] = "limit", ["in"] = "query", ["required"] = false, ["schema"] = new Dictionary<string, object> { ["type"] = "integer" } }
                    },
                    ["responses"] = new Dictionary<string, object>
                    {
                        ["200"] = new Dictionary<string, object>
                        {
                            ["description"] = "Gallery entries",
                            ["content"] = new Dictionary<string, object>
                            {
                                ["application/json"] = new Dictionary<string, object>
                                {
                                    ["schema"] = new Dictionary<string, object>
                                    {
                                        ["type"] = "array",
                                        ["items"] = new Dictionary<string, object> { ["$ref"] = "#/components/schemas/GalleryListItem" }
                                    }
                                }
                            }
                        }
                    }
                }
            },
            ["/api/gallery/{name}"] = new Dictionary<string, object>
            {
                ["get"] = new Dictionary<string, object>
                {
                    ["summary"] = "Fetch stored gallery HTML",
                    ["operationId"] = "getGalleryHTML",
                    ["parameters"] = new object[]
                    {
                        new Dictionary<string, object> { ["name"] = "name", ["in"] = "path", ["required"] = true, ["schema"] = new Dictionary<string, object> { ["type"] = "string" } }
                    },
                    ["responses"] = new Dictionary<string, object>
                    {
                        ["200"] = new Dictionary<string, object>
                        {
                            ["description"] = "Stored HTML file",
                            ["content"] = new Dictionary<string, object> { ["text/html"] = new Dictionary<string, object>() }
                        },
                        ["404"] = new Dictionary<string, object> { ["description"] = "Gallery file not found" }
                    }
                }
            },
            ["/api/activity"] = new Dictionary<string, object>
            {
                ["get"] = new Dictionary<string, object>
                {
                    ["summary"] = "Get activity snapshot",
                    ["operationId"] = "getActivity",
                    ["parameters"] = new object[]
                    {
                        TenantQueryParameter()
                    },
                    ["responses"] = new Dictionary<string, object>
                    {
                        ["200"] = new Dictionary<string, object>
                        {
                            ["description"] = "Activity snapshot",
                            ["content"] = new Dictionary<string, object>
                            {
                                ["application/json"] = new Dictionary<string, object>
                                {
                                    ["schema"] = new Dictionary<string, object> { ["$ref"] = "#/components/schemas/ActivityAPIView" }
                                }
                            }
                        }
                    }
                }
            },
            ["/api/tenants"] = new Dictionary<string, object>
            {
                ["get"] = new Dictionary<string, object>
                {
                    ["summary"] = "List readable tenants",
                    ["operationId"] = "listTenants",
                    ["responses"] = new Dictionary<string, object>
                    {
                        ["200"] = new Dictionary<string, object>
                        {
                            ["description"] = "Current tenant and readable tenant list",
                            ["content"] = new Dictionary<string, object>
                            {
                                ["application/json"] = new Dictionary<string, object>
                                {
                                    ["schema"] = new Dictionary<string, object> { ["$ref"] = "#/components/schemas/TenantsView" }
                                }
                            }
                        }
                    }
                }
            }
        },
        ["components"] = new Dictionary<string, object>
        {
            ["schemas"] = new Dictionary<string, object>
            {
                ["ToolCountView"] = new Dictionary<string, object>
                {
                    ["type"] = "object",
                    ["properties"] = new Dictionary<string, object>
                    {
                        ["name"] = new Dictionary<string, object> { ["type"] = "string" },
                        ["count"] = new Dictionary<string, object> { ["type"] = "integer" }
                    },
                    ["required"] = new[] { "name", "count" }
                },
                ["ActivityAPIView"] = new Dictionary<string, object>
                {
                    ["type"] = "object",
                    ["properties"] = new Dictionary<string, object>
                    {
                        ["tenant"] = new Dictionary<string, object> { ["type"] = "string" },
                        ["activity"] = new Dictionary<string, object> { ["$ref"] = "#/components/schemas/ActivityScopesView" },
                        ["tools"] = new Dictionary<string, object> { ["$ref"] = "#/components/schemas/ToolBreakdownScopesView" }
                    },
                    ["required"] = new[] { "tenant", "activity", "tools" }
                },
                ["ActivityScopesView"] = new Dictionary<string, object>
                {
                    ["type"] = "object",
                    ["properties"] = new Dictionary<string, object>
                    {
                        ["current_tenant"] = new Dictionary<string, object> { ["$ref"] = "#/components/schemas/ActivityCountsView" },
                        ["all_tenants"] = new Dictionary<string, object> { ["$ref"] = "#/components/schemas/ActivityCountsView" }
                    },
                    ["required"] = new[] { "current_tenant", "all_tenants" }
                },
                ["ActivityCountsView"] = new Dictionary<string, object>
                {
                    ["type"] = "object",
                    ["properties"] = new Dictionary<string, object>
                    {
                        ["session_count"] = new Dictionary<string, object> { ["type"] = "integer" },
                        ["user_count"] = new Dictionary<string, object> { ["type"] = "integer" },
                        ["tool_calls"] = new Dictionary<string, object> { ["type"] = "integer" },
                        ["distinct_tools_called"] = new Dictionary<string, object> { ["type"] = "integer" },
                        ["agent_stops"] = new Dictionary<string, object> { ["type"] = "integer" },
                        ["subagent_stops"] = new Dictionary<string, object> { ["type"] = "integer" },
                        ["uploaded_games_count"] = new Dictionary<string, object> { ["type"] = "integer" }
                    },
                    ["required"] = new[] { "session_count", "user_count", "tool_calls", "distinct_tools_called", "agent_stops", "subagent_stops", "uploaded_games_count" }
                },
                ["ToolBreakdownScopesView"] = new Dictionary<string, object>
                {
                    ["type"] = "object",
                    ["properties"] = new Dictionary<string, object>
                    {
                        ["current_tenant"] = new Dictionary<string, object>
                        {
                            ["type"] = "array",
                            ["items"] = new Dictionary<string, object> { ["$ref"] = "#/components/schemas/ToolCountView" }
                        },
                        ["all_tenants"] = new Dictionary<string, object>
                        {
                            ["type"] = "array",
                            ["items"] = new Dictionary<string, object> { ["$ref"] = "#/components/schemas/ToolCountView" }
                        }
                    },
                    ["required"] = new[] { "current_tenant", "all_tenants" }
                },
                ["GalleryListItem"] = new Dictionary<string, object>
                {
                    ["type"] = "object",
                    ["properties"] = new Dictionary<string, object>
                    {
                        ["name"] = new Dictionary<string, object> { ["type"] = "string" },
                        ["url"] = new Dictionary<string, object> { ["type"] = "string" }
                    },
                    ["required"] = new[] { "name", "url" }
                },
                ["TenantsView"] = new Dictionary<string, object>
                {
                    ["type"] = "object",
                    ["properties"] = new Dictionary<string, object>
                    {
                        ["current_tenant"] = new Dictionary<string, object> { ["type"] = "string" },
                        ["tenants"] = new Dictionary<string, object>
                        {
                            ["type"] = "array",
                            ["items"] = new Dictionary<string, object> { ["type"] = "string" }
                        }
                    },
                    ["required"] = new[] { "current_tenant", "tenants" }
                }
            }
        }
    };

    private static Dictionary<string, object> TenantQueryParameter() => new()
    {
        ["name"] = "tenant",
        ["in"] = "query",
        ["required"] = false,
        ["description"] = "Tenant to read. Omit to read the configured current tenant.",
        ["schema"] = new Dictionary<string, object> { ["type"] = "string", ["pattern"] = "^[a-zA-Z0-9][a-zA-Z0-9-]{0,30}$" }
    };
}
