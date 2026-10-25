var builder = DistributedApplication.CreateBuilder(args);

// The engine refuses a request without this key. FellowshipSim.Service is the only other resource with it.
var engineKey = builder.AddParameter("engine-key", new GenerateParameterDefault { MinLength = 32, Special = false }, secret: true);

var engine = builder.AddProject<Projects.FellowshipSim_EngineServer>("engine")
    .WithEnvironment("EngineKey", engineKey);

builder.AddProject<Projects.FellowshipSim_Service>("service")
    .WithEnvironment("EngineKey", engineKey)
    .WithReference(engine)
    .WaitFor(engine)
    .WithEndpoint("http", e =>
    {
        e.TargetHost = "simfell.dev.localhost";
        e.Port = 5000;
    })
    .WithExternalHttpEndpoints();

builder.Build().Run();