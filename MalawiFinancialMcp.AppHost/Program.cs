var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres")
    .WithDataVolume("malawi-postgres-data")
    .WithPgAdmin();

var db = postgres.AddDatabase("malawi-financial");

var mcpServer = builder.AddProject<Projects.MalawiFinancialMcp>("mcp-server")
    .WithReference(db)
    .WaitFor(db);

var api = builder.AddProject<Projects.MalawiFinancialApi>("api")
    .WithReference(db)
    .WaitFor(db);

builder.Build().Run();
