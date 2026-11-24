# Dev Bench API

- Standalone ASP.NET Core .NET 10 backend. No Node.js or desktop source dependencies.
- Communicate and document delivery in Vietnamese. API/browser messages in English.
- Keep HTTP contracts in docs/api-contract.md compatible with the Electron client.
- Production DI always uses MongoCloudStore and GoogleProvider. Fake identity and
  MemoryCloudStore belong only in the test executable; never add production login bypasses.
- Preserve Google state/nonce/S256, one-use login exchange and per-device session hashes.
- All backup queries use userId from the authenticated session, never client ownership fields.
- Do not log secrets, authorization headers, OAuth codes or token endpoint bodies.
- Run dotnet build and dotnet run --project tests/DevBench.Api.Tests before delivery.
- Set TEST_MONGODB_URI to a local test MongoDB for full integration. Tests create/drop
  only randomly named dev_bench_test_* databases. Real Google sign-in is manual.
