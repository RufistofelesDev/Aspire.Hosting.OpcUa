var builder = DistributedApplication.CreateBuilder(args);

// A factory floor, in your Aspire dashboard. No PLC required.
builder.AddOpcUaPlc("plc")
       .WithFastNodes(5, TimeSpan.FromSeconds(1))
       .WithSlowNodes(5, TimeSpan.FromSeconds(10))
       .WithGuidNodes(5)
       .WithAlarms();

builder.Build().Run();
