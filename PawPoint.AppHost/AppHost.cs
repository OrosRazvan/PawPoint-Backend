var builder = DistributedApplication.CreateBuilder(args);

var db = builder.AddPostgres("db")
    .WithDataVolume()
    .WithPgWeb()
    .AddDatabase("PawPointDB");

var storage = builder.AddAzureStorage("storage");
var blobs = storage.AddBlobs("profile-pics");
var filesBlobs = storage.AddBlobs("data-updates");
storage.RunAsEmulator(az => az.WithDataVolume());

var _ = builder.AddProject<Projects.PawPoint_ApiServices>("apiservice")
    .WithReference(db)
    .WithReference(blobs)
    .WithReference(filesBlobs)
    .WaitFor(db);

builder.Build().Run();