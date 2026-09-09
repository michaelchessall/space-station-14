using Robust.Shared.Serialization;
using Robust.Shared.Serialization.Manager;
using Robust.Shared.Serialization.Markdown.Mapping;
using Robust.Shared.Serialization.Markdown.Sequence;
using Robust.Shared.Serialization.Markdown.Validation;
using Robust.Shared.Serialization.TypeSerializers.Interfaces;

namespace Content.Shared._Persistence14.Cargo;

[TypeSerializer]
public sealed class BountyConditionMappingSerializer : ITypeReader<BountyCondition, MappingDataNode>
{
    private Type? GetType(MappingDataNode node)
    {
        Type? type = null;

        void SetType(Type newType)
        {
            if (type != null)
                throw new ArgumentException("Bounty condition mapping matches multiple condition types.");

            type = newType;
        }

        if (node.Has("whitelist") || node.Has("blacklist"))
            SetType(typeof(BountyConditionEntityWhitelist));

        if (node.Has("gas"))
            SetType(typeof(BountyConditionGas));

        if (node.Has("reagent"))
            SetType(typeof(BountyConditionReagent));

        return type;
    }

    public BountyCondition Read(
        ISerializationManager serializationManager,
        MappingDataNode node,
        IDependencyCollection dependencies,
        SerializationHookContext hookCtx,
        ISerializationContext? context = null,
        ISerializationManager.InstantiationDelegate<BountyCondition>? instanceProvider = null)
    {
        var type = GetType(node);
        if (type is null)
            throw new ArgumentException("Tried to convert invalid YAML node mapping to BountyCondition!");

        return (BountyCondition)serializationManager.Read(type, node, hookCtx, context)!;
    }

    public ValidationNode Validate(
        ISerializationManager serializationManager,
        MappingDataNode node,
        IDependencyCollection dependencies,
        ISerializationContext? context = null)
    {
        var type = GetType(node);
        if (type is null)
            return new ErrorNode(node, "No bounty condition type found.");

        return serializationManager.ValidateNode(type, node, context);
    }
}

[TypeSerializer]
public sealed class BountyConditionSequenceSerializer : ITypeReader<BountyCondition, SequenceDataNode>
{
    public BountyCondition Read(
        ISerializationManager serializationManager,
        SequenceDataNode node,
        IDependencyCollection dependencies,
        SerializationHookContext hookCtx,
        ISerializationContext? context = null,
        ISerializationManager.InstantiationDelegate<BountyCondition>? instanceProvider = null)
    {
        var conditions = serializationManager.Read<List<BountyCondition>>(node, hookCtx, context, notNullableOverride: true)!;

        return new BountyConditionAll(conditions.ToArray());
    }

    public ValidationNode Validate(
        ISerializationManager serializationManager,
        SequenceDataNode node,
        IDependencyCollection dependencies,
        ISerializationContext? context = null)
    {
        return serializationManager.ValidateNode<List<BountyCondition>>(node, context);
    }
}