using System.Numerics;
using Simple3dRenderer.Objects;

namespace Simple3dRenderer.VoxelMap
{
    public class VoxelGrid
    {
        private readonly Dictionary<(int, int, int), Voxel> voxels = [];
        private readonly float voxelSize;

        public VoxelGrid(float voxelSize)
        {
            if (voxelSize <= 0)
                throw new ArgumentOutOfRangeException(nameof(voxelSize), "Voxel size must be positive.");
            this.voxelSize = voxelSize;
        }

        // Convert world position to voxel coordinate
        private (int X, int Y, int Z) WorldToVoxel(Vector3 pos)
        {
            return (
                (int)MathF.Floor(pos.X / voxelSize),
                (int)MathF.Floor(pos.Y / voxelSize),
                (int)MathF.Floor(pos.Z / voxelSize)
            );
        }

        // Get voxel bounds in world space
        private (Vector3 min, Vector3 max) GetVoxelBounds((int x, int y, int z) coord)
        {
            Vector3 min = new(coord.x * voxelSize, coord.y * voxelSize, coord.z * voxelSize);
            Vector3 max = min + new Vector3(voxelSize);
            return (min, max);
        }

        // Add a mesh to the voxel grid
        public void AddMesh(Mesh mesh)
        {
            var minCoord = WorldToVoxel(mesh.WorldBoundsMin);
            var maxCoord = WorldToVoxel(mesh.WorldBoundsMax);

            for (int x = minCoord.X; x <= maxCoord.X; x++)
            {
                for (int y = minCoord.Y; y <= maxCoord.Y; y++)
                {
                    for (int z = minCoord.Z; z <= maxCoord.Z; z++)
                    {
                        var key = (x, y, z);
                        if (!voxels.TryGetValue(key, out var voxel))
                        {
                            voxel = new Voxel(new List<Mesh>());
                            voxels[key] = voxel;
                        }

                        // Avoid duplicates
                        if (!voxel.MeshIntersections.Contains(mesh))
                            voxel.MeshIntersections.Add(mesh);
                    }
                }
            }
        }

        // Remove a mesh from the voxel grid
        public void RemoveMesh(Mesh mesh)
        {
            var minCoord = WorldToVoxel(mesh.WorldBoundsMin);
            var maxCoord = WorldToVoxel(mesh.WorldBoundsMax);

            for (int x = minCoord.X; x <= maxCoord.X; x++)
            {
                for (int y = minCoord.Y; y <= maxCoord.Y; y++)
                {
                    for (int z = minCoord.Z; z <= maxCoord.Z; z++)
                    {
                        var key = (x, y, z);
                        if (voxels.TryGetValue(key, out var voxel))
                        {
                            voxel.MeshIntersections.Remove(mesh);

                            // Cleanup empty voxels to save memory
                            if (voxel.MeshIntersections.Count == 0)
                                voxels.Remove(key);
                        }
                    }
                }
            }
        }

        // Checks if a given world position is inside a solid voxel
        public bool IsSolidAt(Vector3 position)
        {
            var coord = WorldToVoxel(position);
            return voxels.TryGetValue(coord, out var voxel) && voxel.IsSolid;
        }

        // Returns all meshes in a given world position's voxel
        public List<Mesh> GetMeshesAt(Vector3 position)
        {
            var coord = WorldToVoxel(position);
            return voxels.TryGetValue(coord, out var voxel) ? voxel.MeshIntersections : new List<Mesh>();
        }

        // Raycast through the voxel grid (rudimentary obstacle detection)
        public Mesh? Raycast(Vector3 origin, Vector3 direction, float maxDistance, float step = 0.5f)
        {
            Vector3 pos = origin;
            float traveled = 0f;

            while (traveled < maxDistance)
            {
                var meshes = GetMeshesAt(pos);
                if (meshes.Count > 0)
                    return meshes[0]; 

                pos += Vector3.Normalize(direction) * step;
                traveled += step;
            }
            return null;
        }
    }
}
