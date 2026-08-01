namespace Aspire.Hosting.OpcUa;

internal static class OpcUaPlcContainerImageTags
{
    /// <remarks>mcr.microsoft.com</remarks>
    internal const string Registry = "mcr.microsoft.com";

    /// <remarks>iotedge/opc-plc</remarks>
    internal const string Image = "iotedge/opc-plc";

    /// <remarks>Pinned rather than <c>latest</c> so a simulator release cannot change test outcomes.</remarks>
    internal const string Tag = "2.14.22";
}
