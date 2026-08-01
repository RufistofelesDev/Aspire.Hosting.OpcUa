# Aspire.Hosting.OpcUa

**A factory floor in your Aspire dashboard. One line. No PLC required.**

```csharp
var builder = DistributedApplication.CreateBuilder(args);

builder.AddOpcUaPlc("plc");

builder.Build().Run();
```

That's a live OPC UA server — the protocol that runs the world's factories, refineries, and power grids — sitting in your app model next to your Postgres and your Redis, behaving like any other Aspire resource.

## Why

If you write industrial software, you already know the problem: **you cannot test an OPC UA client without an OPC UA server**, and servers live in factories. The [OPC Foundation's own compliance tooling](https://opcfoundation.org/developer-tools/certification-test-tools/opc-ua-compliance-test-tool-uactt/) concedes the process "gets very tedious" and must be "repeated over and over for regression testing." The simulators that solve this are commercial, GUI-first, and allergic to CI.

If you *don't* write industrial software, you've probably never seen an address space, and the on-ramp is a 200-page spec.

This package is one line of C# for both of you.

## Install

```bash
dotnet add package Rufistofeles.Aspire.Hosting.OpcUa
```

Requires Aspire 13.x and a container runtime (Docker or Podman).

## Configure

```csharp
builder.AddOpcUaPlc("plc")
       .WithFastNodes(5, TimeSpan.FromSeconds(1))   // values that change every second
       .WithSlowNodes(5, TimeSpan.FromSeconds(10))  // values that drift
       .WithGuidNodes(5)                            // stable node IDs across restarts
       .WithAlarms();                               // alarm & condition nodes
```

Reference it like anything else in Aspire:

```csharp
builder.AddProject<Projects.MyIndustrialApi>("api")
       .WithReference(plc);
```

Your service receives `ConnectionStrings__plc` = `opc.tcp://localhost:50000`.

| Method | Simulator flag | Purpose |
| --- | --- | --- |
| `WithFastNodes(count, rate)` | `--fn`, `--fr` | Nodes updating on a fast cadence |
| `WithSlowNodes(count, rate)` | `--sn`, `--sr` | Nodes updating on a slow cadence |
| `WithGuidNodes(count)` | `--gn` | Deterministic GUID-addressed nodes |
| `WithAlarms()` | `--alm` | Alarm and condition simulation |
| `WithNodesFile(path)` | `--nf` | Your own nodeset, bind-mounted read-only |
| `WithPlcHostname(name)` | `--ph` | Hostname advertised in endpoint descriptors |

### About `WithPlcHostname`

An OPC UA client validates the endpoint URL the server returns during discovery against the one it dialed. When those disagree, the TCP connection succeeds and then the session handshake dies — which is the single most common way a containerized OPC UA server appears "broken." If your client runs outside the container network, set a hostname both sides can resolve.

## What's under it

A thin, typed, discoverable wrapper over [`mcr.microsoft.com/iotedge/opc-plc`](https://github.com/Azure-Samples/iot-edge-opc-plc) — Microsoft's OPC UA PLC simulator, pinned to a known tag rather than `latest` so a simulator release can never change your test outcomes.

This package deliberately owns very little. Microsoft maintains the simulator and patches its CVEs; this repo owns about 200 lines of resource definition. That's the whole design.

## Security

The defaults (`--autoaccept`, `--unsecuretransport`) exist so that `AddOpcUaPlc("plc")` just works in a dev inner loop, without a certificate exchange. They are **development defaults**. The upstream simulator is explicitly not recommended for production deployment, and neither is this.

## License

MIT
