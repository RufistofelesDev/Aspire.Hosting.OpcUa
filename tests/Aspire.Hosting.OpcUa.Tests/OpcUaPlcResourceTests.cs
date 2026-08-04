using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting.OpcUa.Tests;

public class OpcUaPlcResourceTests
{
    [Fact]
    public void AddOpcUaPlc_RegistersResourceWithGivenName()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddOpcUaPlc("plc");

        var app = builder.Build();
        var resource = Assert.Single(app.Services
            .GetRequiredService<DistributedApplicationModel>()
            .Resources.OfType<OpcUaPlcResource>());

        Assert.Equal("plc", resource.Name);
    }

    [Fact]
    public void AddOpcUaPlc_UsesPinnedMicrosoftSimulatorImage()
    {
        var builder = DistributedApplication.CreateBuilder();

        var plc = builder.AddOpcUaPlc("plc");

        var image = Assert.Single(plc.Resource.Annotations.OfType<ContainerImageAnnotation>());
        Assert.Equal("mcr.microsoft.com", image.Registry);
        Assert.Equal("iotedge/opc-plc", image.Image);
        Assert.NotEqual("latest", image.Tag);
    }

    [Fact]
    public void AddOpcUaPlc_ExposesOpcTcpAndHttpEndpoints()
    {
        var builder = DistributedApplication.CreateBuilder();

        var plc = builder.AddOpcUaPlc("plc");

        var endpoints = plc.Resource.Annotations.OfType<EndpointAnnotation>().ToList();

        var opcUa = Assert.Single(endpoints, e => e.Name == "opcua");
        Assert.Equal(50000, opcUa.TargetPort);

        var http = Assert.Single(endpoints, e => e.Name == "http");
        Assert.Equal(8080, http.TargetPort);
        Assert.Equal("http", http.UriScheme);
    }

    [Fact]
    public void AddOpcUaPlc_HonorsExplicitHostPorts()
    {
        var builder = DistributedApplication.CreateBuilder();

        var plc = builder.AddOpcUaPlc("plc", port: 51000, httpPort: 8081);

        var endpoints = plc.Resource.Annotations.OfType<EndpointAnnotation>().ToList();

        Assert.Equal(51000, Assert.Single(endpoints, e => e.Name == "opcua").Port);
        Assert.Equal(8081, Assert.Single(endpoints, e => e.Name == "http").Port);
    }

    [Fact]
    public async Task ConnectionString_IsAnOpcTcpUrl()
    {
        var builder = DistributedApplication.CreateBuilder();

        var plc = builder.AddOpcUaPlc("plc", port: 51000)
                         .WithEndpoint("opcua", e => e.AllocatedEndpoint = new(e, "localhost", 51000));

        var connectionString = await plc.Resource.ConnectionStringExpression.GetValueAsync(TestContext.Current.CancellationToken);

        Assert.Equal("opc.tcp://localhost:51000", connectionString);
    }

    [Fact]
    public async Task AddOpcUaPlc_DefaultsToDevFriendlyTransportArgs()
    {
        var builder = DistributedApplication.CreateBuilder();

        var plc = builder.AddOpcUaPlc("plc");

        var args = await ResolveArgsAsync(plc.Resource);

        Assert.Contains("--autoaccept", args);
        Assert.Contains("--unsecuretransport", args);
        Assert.Contains("--pn=50000", args);
        Assert.Contains("--wp=8080", args);
    }

    [Fact]
    public async Task AddOpcUaPlc_StartsTheWebServerItAdvertises()
    {
        // --wp only picks the port. Without --sp the simulator never opens a web
        // listener, so the HTTP endpoint below would be declared and dead.
        var builder = DistributedApplication.CreateBuilder();

        var plc = builder.AddOpcUaPlc("plc");

        var args = await ResolveArgsAsync(plc.Resource);

        Assert.Contains("--sp", args);
        Assert.Contains(plc.Resource.Annotations.OfType<EndpointAnnotation>(), e => e.Name == "http");
    }

    [Fact]
    public async Task WithFastNodes_EmitsCountAndRate()
    {
        var builder = DistributedApplication.CreateBuilder();

        var plc = builder.AddOpcUaPlc("plc").WithFastNodes(5, TimeSpan.FromSeconds(2));

        var args = await ResolveArgsAsync(plc.Resource);

        Assert.Contains("--fn=5", args);
        Assert.Contains("--fr=2", args);
    }

    [Fact]
    public async Task WithSlowNodes_EmitsCountAndRate()
    {
        var builder = DistributedApplication.CreateBuilder();

        var plc = builder.AddOpcUaPlc("plc").WithSlowNodes(3, TimeSpan.FromSeconds(30));

        var args = await ResolveArgsAsync(plc.Resource);

        Assert.Contains("--sn=3", args);
        Assert.Contains("--sr=30", args);
    }

    [Fact]
    public async Task WithFastNodes_OmitsRateWhenNotSpecified()
    {
        var builder = DistributedApplication.CreateBuilder();

        var plc = builder.AddOpcUaPlc("plc").WithFastNodes(5);

        var args = await ResolveArgsAsync(plc.Resource);

        Assert.Contains("--fn=5", args);
        Assert.DoesNotContain(args, a => a.StartsWith("--fr=", StringComparison.Ordinal));
    }

    [Fact]
    public async Task WithAlarmsAndGuidNodes_EmitFlags()
    {
        var builder = DistributedApplication.CreateBuilder();

        var plc = builder.AddOpcUaPlc("plc").WithAlarms().WithGuidNodes(4);

        var args = await ResolveArgsAsync(plc.Resource);

        Assert.Contains("--alm", args);
        Assert.Contains("--gn=4", args);
    }

    [Fact]
    public async Task WithPlcHostname_EmitsAdvertisedHostname()
    {
        var builder = DistributedApplication.CreateBuilder();

        var plc = builder.AddOpcUaPlc("plc").WithPlcHostname("plc.local");

        var args = await ResolveArgsAsync(plc.Resource);

        Assert.Contains("--ph=plc.local", args);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(999)] // Sub-second rates truncate to zero, which the simulator would reject.
    public void WithFastNodes_RejectsRatesUnderOneSecond(int milliseconds)
    {
        var builder = DistributedApplication.CreateBuilder();
        var plc = builder.AddOpcUaPlc("plc");

        Assert.Throws<ArgumentOutOfRangeException>(
            () => plc.WithFastNodes(5, TimeSpan.FromMilliseconds(milliseconds)));
    }

    [Fact]
    public void WithSlowNodes_RejectsNegativeCount()
    {
        var builder = DistributedApplication.CreateBuilder();
        var plc = builder.AddOpcUaPlc("plc");

        Assert.Throws<ArgumentOutOfRangeException>(() => plc.WithSlowNodes(-1));
    }

    [Fact]
    public void WithNodesFile_RejectsBlankPath()
    {
        var builder = DistributedApplication.CreateBuilder();
        var plc = builder.AddOpcUaPlc("plc");

        Assert.Throws<ArgumentException>(() => plc.WithNodesFile("   "));
    }

    private static async Task<List<string>> ResolveArgsAsync(OpcUaPlcResource resource)
    {
        var context = new CommandLineArgsCallbackContext([], TestContext.Current.CancellationToken);

        foreach (var annotation in resource.Annotations.OfType<CommandLineArgsCallbackAnnotation>())
        {
            await annotation.Callback(context);
        }

        var resolved = new List<string>();

        foreach (var arg in context.Args)
        {
            resolved.Add(arg switch
            {
                string s => s,
                IValueProvider provider => await provider.GetValueAsync(TestContext.Current.CancellationToken) ?? string.Empty,
                _ => arg.ToString() ?? string.Empty,
            });
        }

        return resolved;
    }
}
