# Dev Bench API

- Standalone ASP.NET Core .NET 10 backend. No Node.js or desktop source dependencies.
- Solution lives at the repository root; API project is DevBench.Api/ (no src/).
  Controllers handle HTTP, Services/Interfaces define business operations, Repositories
  own persistence, and Models/Dtos contain HTTP contracts.
- Keep real credentials out of committed appsettings and example files. Local setup
  and Azure environment variable mappings are documented in docs/configuration.md.
- Communicate and document delivery in Vietnamese. API/browser messages in English.
- Keep HTTP contracts in docs/api-contract.md compatible with the Electron client.
- Read CLAUDE.md and relevant .claude/rules before editing; they document backend
  conventions and the controller/service/repository workflow.
- Production DI always uses MongoCloudStore and GoogleProvider; never add fake
  identity, in-memory production fallbacks or login bypasses.
- Preserve Google state/nonce/S256, one-use login exchange and per-device session hashes.
- All backup queries use userId from the authenticated session, never client ownership fields.
- Do not log secrets, authorization headers, OAuth codes or token endpoint bodies.
- This backend has no tests/ folder or test project; do not recreate them unless
  requested. Do not run or prescribe commands for the removed test harness.
- For code/project/build changes, run dotnet build -c Release and publish the API.
  For documentation-only changes, check content, links and references as appropriate.
- Smoke-check with MongoDB and real Google credentials only when available and
  authorized. Record checks actually run; never create/delete fixture data in a real DB.
