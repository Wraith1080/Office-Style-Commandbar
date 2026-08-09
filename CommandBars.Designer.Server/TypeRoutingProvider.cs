using Microsoft.DotNet.DesignTools.TypeRouting;

namespace CommandBars.Designer.Server;

/// <summary>
/// Server-side type routing: maps short designer names to the actual designer
/// types living in this assembly. The client side (Visual Studio) only ever
/// sees component proxies and designer names as strings; when it needs a
/// designer, the server resolves the name through this table.
/// </summary>
[ExportTypeRoutingDefinitionProvider]
internal class TypeRoutingProvider : TypeRoutingDefinitionProvider
{
    public override IEnumerable<TypeRoutingDefinition> GetDefinitions()
        => new[]
        {
            new TypeRoutingDefinition(
                TypeRoutingKinds.Designer,
                nameof(CommandBarManagerDesigner),
                typeof(CommandBarManagerDesigner)),
            new TypeRoutingDefinition(
                TypeRoutingKinds.Designer,
                nameof(DockHostDesigner),
                typeof(DockHostDesigner)),
            new TypeRoutingDefinition(
                TypeRoutingKinds.Designer,
                nameof(SvgImageListDesigner),
                typeof(SvgImageListDesigner)),
        };
}
