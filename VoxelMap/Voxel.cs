using Simple3dRenderer.Objects;
namespace Simple3dRenderer.VoxelMap
{
    record struct Voxel(List<Mesh> MeshIntersections)
    {
        public readonly bool IsSolid => MeshIntersections.Count > 0;
    };

}
