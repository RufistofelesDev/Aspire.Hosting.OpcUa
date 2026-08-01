namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// An OPC UA PLC simulator resource, backed by the <c>mcr.microsoft.com/iotedge/opc-plc</c> container.
/// </summary>
/// <param name="name">The resource name.</param>
public sealed class OpcUaPlcResource([ResourceName] string name) : ContainerResource(name), IResourceWithConnectionString
{
    internal const string OpcUaEndpointName = "opcua";
    internal const string HttpEndpointName = "http";

    private EndpointReference? _opcUaReference;
    private EndpointReference? _httpReference;

    /// <summary>
    /// The <c>opc.tcp</c> endpoint clients connect to.
    /// </summary>
    public EndpointReference OpcUaEndpoint => _opcUaReference ??= new(this, OpcUaEndpointName);

    /// <summary>
    /// The simulator's web endpoint, which serves the generated OPC Publisher configuration.
    /// </summary>
    public EndpointReference HttpEndpoint => _httpReference ??= new(this, HttpEndpointName);

    /// <summary>
    /// The OPC UA discovery URL, in the form <c>opc.tcp://host:port</c>.
    /// </summary>
    /// <remarks>
    /// The simulator advertises its endpoints using the hostname it was started with, so consumers
    /// resolving this connection string from outside the container network must run the resource
    /// with <see cref="OpcUaPlcBuilderExtensions.WithPlcHostname"/> set to a name they can also resolve.
    /// </remarks>
    public ReferenceExpression ConnectionStringExpression =>
        ReferenceExpression.Create($"opc.tcp://{OpcUaEndpoint.Property(EndpointProperty.HostAndPort)}");
}
