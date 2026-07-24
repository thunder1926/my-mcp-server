# MyMcpServer

A minimal MCP (Model Context Protocol) server in C# / .NET 10, exposing:

- **FileTools** — sandboxed read/write/list inside one allowed workspace folder
- **BuildTools** — `dotnet build` / `dotnet test` wrappers
- **ExternalApiTools** — example calls to an external HTTP API

It talks to clients (Claude Desktop, Claude Code, Cursor, Codex) over **stdio** —
the client launches your exe as a subprocess and talks JSON-RPC over
stdin/stdout. No networking/auth needed for local use.

---

## 1. Open and build

1. Open the `MyMcpServer` folder in Visual Studio 2022 (17.12+) or run from CLI.
2. Make sure you have the **.NET 10 SDK** installed: https://dotnet.microsoft.com/download/dotnet/10.0
3. Restore + build:
   ```bash
   cd MyMcpServer
   dotnet restore
   dotnet build
   ```
4. Check the `ModelContextProtocol` package version in `MyMcpServer.csproj` —
   it's a fast-moving preview package, so run this once to grab the latest:
   ```bash
   dotnet add package ModelContextProtocol
   ```

## 2. Configure it for your use case

- **FileTools.cs**: set `MCP_ALLOWED_ROOT` env var (or edit the default path)
  to the one folder you want the agent allowed to touch. Never widen this to
  a whole drive.
- **ExternalApiTools.cs**: change the `BaseAddress` in `Program.cs`'s
  `AddHttpClient("external-api", ...)` to your real API, and add an
  `Authorization` header there if it needs a key.
- **BuildTools.cs**: works as-is for any `.csproj`/`.sln` path you pass in.

## 3. Test it standalone (optional but recommended)

Install the MCP Inspector (Node-based dev tool) to poke at your server
before wiring it into a real client:
```bash
npx @modelcontextprotocol/inspector dotnet run --project MyMcpServer.csproj
```
This opens a browser UI where you can call each tool directly and see
raw request/response JSON.

## 4. Publish as a standalone executable (recommended over `dotnet run`)

Faster startup, and the target machine doesn't need the SDK installed:
```bash
dotnet publish -c Release -r win-x64 --self-contained -p:PublishAot=true
```
(swap `win-x64` for `linux-x64` / `osx-arm64` as needed). The exe lands in
`bin/Release/net10.0/win-x64/publish/MyMcpServer.exe`.

---

## 5. Connect it to Claude Desktop

Edit (or create) the config file:
- **Windows**: `%APPDATA%\Claude\claude_desktop_config.json`
- **macOS**: `~/Library/Application Support/Claude/claude_desktop_config.json`

```json
{
  "mcpServers": {
    "my-csharp-server": {
      "command": "C:\\path\\to\\MyMcpServer\\bin\\Release\\net10.0\\win-x64\\publish\\MyMcpServer.exe",
      "env": {
        "MCP_ALLOWED_ROOT": "C:\\Users\\you\\mcp-workspace"
      }
    }
  }
}
```
Or, while developing, point `command` at `dotnet` with `args: ["run", "--project", "C:\\path\\to\\MyMcpServer.csproj"]` instead — slower to start but no publish step needed.

Restart Claude Desktop. Your tools should show up under the 🔨 tool icon.

## 6. Connect it to Cursor

Cursor Settings → **MCP** → **Add new MCP server**. Same shape:
```json
{
  "mcpServers": {
    "my-csharp-server": {
      "command": "dotnet",
      "args": ["run", "--project", "/absolute/path/to/MyMcpServer.csproj"]
    }
  }
}
```
Cursor stores this in `.cursor/mcp.json` (project-level) or its global
settings file — either works.

## 7. Connect it to Codex CLI

Codex reads MCP servers from its own config (`~/.codex/config.toml` or
equivalent depending on version) using the same `command`/`args` shape.
Check `codex mcp --help` or its docs for the exact key name in your
installed version, since this has changed across releases.

---

## Safety notes before you rely on this

- `FileTools` refuses any path that resolves outside `MCP_ALLOWED_ROOT` —
  don't remove that check.
- `BuildTools` runs whatever `dotnet build`/`test` finds at the path you
  pass — don't expose it to arbitrary untrusted paths.
- Consider adding a confirmation/dry-run flag before adding any tool that
  deploys or deletes something for real.
- All logs go to **stderr**, not stdout — stdout is reserved for MCP
  protocol messages. Don't add `Console.WriteLine` anywhere in tool code.
