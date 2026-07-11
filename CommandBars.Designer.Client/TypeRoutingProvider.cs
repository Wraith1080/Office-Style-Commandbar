using System.Collections.Generic;
using Microsoft.DotNet.DesignTools.Client.TypeRouting;
using CommandBars.Designer.Protocol.Endpoints;

namespace CommandBars.Designer.Client;

/// <summary>
/// Client-side routing: maps the editor names referenced by
/// <c>[Editor("name", typeof(UITypeEditor))]</c> on the runtime properties to
/// the actual client <see cref="System.Drawing.Design.UITypeEditor"/> types in
/// this assembly. Without this, the property browser (running in VS) can't bind
/// the string editor name to a real editor, so nothing opens.
/// </summary>
[ExportTypeRoutingDefinitionProvider]
internal class TypeRoutingProvider : TypeRoutingDefinitionProvider
{
    public override IEnumerable<TypeRoutingDefinition> GetDefinitions()
        => new[]
        {
            new TypeRoutingDefinition(
                TypeRoutingKinds.Editor,
                EditorNames.BarDefinitionsEditor,
                typeof(BarDefinitionsEditor)),
            new TypeRoutingDefinition(
                TypeRoutingKinds.Editor,
                EditorNames.SvgMarkupEditor,
                typeof(SvgMarkupEditor)),
        };
}
