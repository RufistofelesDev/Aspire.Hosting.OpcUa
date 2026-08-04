using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.OpcUa;

namespace Aspire.Hosting;

/// <summary>
/// Adds an OPC UA PLC simulator to an Aspire application model.
/// </summary>
public static class OpcUaPlcBuilderExtensions
{
    private const int OpcUaTargetPort = 50000;
    private const int HttpTargetPort = 8080;

    /// <summary>
    /// Adds an OPC UA PLC simulator container to the application model.
    /// </summary>
    /// <param name="builder">The distributed application builder.</param>
    /// <param name="name">The resource name.</param>
    /// <param name="port">The host port for the <c>opc.tcp</c> endpoint. Random when <see langword="null"/>.</param>
    /// <param name="httpPort">The host port for the simulator's web endpoint. Random when <see langword="null"/>.</param>
    /// <returns>A builder for the <see cref="OpcUaPlcResource"/>.</returns>
    /// <remarks>
    /// Defaults are tuned for local development: the simulator accepts any client certificate
    /// (<c>--autoaccept</c>) and permits an unsecured transport (<c>--unsecuretransport</c>) so a
    /// client can connect without a certificate exchange. Neither is appropriate outside a dev inner loop.
    /// </remarks>
    public static IResourceBuilder<OpcUaPlcResource> AddOpcUaPlc(
        this IDistributedApplicationBuilder builder,
        [ResourceName] string name,
        int? port = null,
        int? httpPort = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(name);

        var resource = new OpcUaPlcResource(name);

        return builder.AddResource(resource)
                      .WithImage(OpcUaPlcContainerImageTags.Image)
                      .WithImageRegistry(OpcUaPlcContainerImageTags.Registry)
                      .WithImageTag(OpcUaPlcContainerImageTags.Tag)
                      .WithEndpoint(targetPort: OpcUaTargetPort, port: port, name: OpcUaPlcResource.OpcUaEndpointName)
                      .WithHttpEndpoint(targetPort: HttpTargetPort, port: httpPort, name: OpcUaPlcResource.HttpEndpointName)
                      .WithArgs($"--pn={OpcUaTargetPort}")
                      .WithArgs($"--wp={HttpTargetPort}")
                      // --wp only chooses the port; the web server does not exist without
                      // --sp. Without this the HTTP endpoint above is declared, appears in
                      // the dashboard, and never answers.
                      .WithArgs("--sp")
                      .WithArgs("--autoaccept")
                      .WithArgs("--unsecuretransport");
    }

    /// <summary>
    /// Configures nodes whose values change on a fast cadence.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <param name="count">The number of fast-changing nodes.</param>
    /// <param name="rate">How often each node updates. Truncated to whole seconds by the simulator.</param>
    public static IResourceBuilder<OpcUaPlcResource> WithFastNodes(
        this IResourceBuilder<OpcUaPlcResource> builder,
        int count,
        TimeSpan? rate = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentOutOfRangeException.ThrowIfNegative(count);

        builder.WithArgs($"--fn={count}");

        if (rate is { } value)
        {
            builder.WithArgs($"--fr={ToWholeSeconds(value, nameof(rate))}");
        }

        return builder;
    }

    /// <summary>
    /// Configures nodes whose values change on a slow cadence.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <param name="count">The number of slow-changing nodes.</param>
    /// <param name="rate">How often each node updates. Truncated to whole seconds by the simulator.</param>
    public static IResourceBuilder<OpcUaPlcResource> WithSlowNodes(
        this IResourceBuilder<OpcUaPlcResource> builder,
        int count,
        TimeSpan? rate = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentOutOfRangeException.ThrowIfNegative(count);

        builder.WithArgs($"--sn={count}");

        if (rate is { } value)
        {
            builder.WithArgs($"--sr={ToWholeSeconds(value, nameof(rate))}");
        }

        return builder;
    }

    /// <summary>
    /// Adds nodes addressed by deterministic GUID node IDs, which stay stable across restarts.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <param name="count">The number of GUID-addressed nodes.</param>
    public static IResourceBuilder<OpcUaPlcResource> WithGuidNodes(
        this IResourceBuilder<OpcUaPlcResource> builder,
        int count)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentOutOfRangeException.ThrowIfNegative(count);

        return builder.WithArgs($"--gn={count}");
    }

    /// <summary>
    /// Enables the simulator's alarm and condition nodes.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    public static IResourceBuilder<OpcUaPlcResource> WithAlarms(this IResourceBuilder<OpcUaPlcResource> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.WithArgs("--alm");
    }

    /// <summary>
    /// Loads a user-defined nodeset from a JSON file on the host.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <param name="path">A host path to the nodes file. Relative paths resolve against the AppHost directory.</param>
    /// <remarks>Binds the file into the container read-only, then points the simulator at it.</remarks>
    public static IResourceBuilder<OpcUaPlcResource> WithNodesFile(
        this IResourceBuilder<OpcUaPlcResource> builder,
        string path)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        const string target = "/app/nodes.json";

        return builder.WithBindMount(path, target, isReadOnly: true)
                      .WithArgs($"--nf={target}");
    }

    /// <summary>
    /// Sets the hostname the simulator advertises in its endpoint descriptors.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <param name="hostname">A hostname the connecting client can also resolve.</param>
    /// <remarks>
    /// An OPC UA client validates the endpoint URL the server returns during discovery against the
    /// one it dialed. When those disagree the session handshake fails, which is the usual cause of a
    /// containerized simulator that accepts a TCP connection and then drops the session.
    /// </remarks>
    public static IResourceBuilder<OpcUaPlcResource> WithPlcHostname(
        this IResourceBuilder<OpcUaPlcResource> builder,
        string hostname)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(hostname);

        return builder.WithArgs($"--ph={hostname}");
    }

    private static int ToWholeSeconds(TimeSpan value, string paramName)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(value, TimeSpan.Zero, paramName);

        var seconds = (int)value.TotalSeconds;
        ArgumentOutOfRangeException.ThrowIfZero(seconds, paramName);

        return seconds;
    }
}
