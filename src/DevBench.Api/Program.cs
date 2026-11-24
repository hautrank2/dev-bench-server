using DevBench.Api;

var app = await ApiApplication.BuildAsync(args);
await app.RunAsync();
