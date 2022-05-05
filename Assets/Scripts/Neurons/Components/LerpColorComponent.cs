using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;

public struct LerpColorComponent : IComponentData
{
    public float4 maxColor;
    public float4 zeroColor;
}