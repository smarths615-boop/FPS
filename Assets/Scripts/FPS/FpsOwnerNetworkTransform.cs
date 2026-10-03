using Unity.Netcode.Components;

public sealed class FpsOwnerNetworkTransform : NetworkTransform
{
    protected override bool OnIsServerAuthoritative() => false;
}
