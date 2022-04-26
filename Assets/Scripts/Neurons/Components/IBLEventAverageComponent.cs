using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;

public struct IBLEventAverageComponent : IComponentData
{
    public FixedList4096Bytes<float> spikeRate;
}