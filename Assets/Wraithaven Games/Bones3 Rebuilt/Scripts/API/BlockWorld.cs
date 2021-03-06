using System.Collections.Generic;

using UnityEngine;

namespace WraithavenGames.Bones3
{
    /// <summary>
    /// The main behaviour for containing a voxel block world.
    /// </summary>
    [AddComponentMenu("Bones3/Block World")]
    [SelectionBase, ExecuteAlways, DisallowMultipleComponent]
    [RequireComponent(typeof(BlockListManager))]
    [RequireComponent(typeof(WorldGenerator))]
    public class BlockWorld : MonoBehaviour
    {
        /// <summary>
        /// The chunk size int bits, such as the actual number of blocks is
        /// 1 << CHUNK_SIZE
        /// 
        /// This value provides a good balance between performance and playability.
        /// Use caution when adjusting.
        /// </summary>
        private const int CHUNK_SIZE = 4;

        [SerializeField, HideInInspector] protected string ID = System.Guid.NewGuid().ToString();

        private UnityWorldBuilder m_UnityWorldBuilder;

        /// <summary>
        /// Gets the world builder instance, creating it if needed.
        /// </summary>
        private UnityWorldBuilder WorldBuilder
        {
            get
            {
                if (m_UnityWorldBuilder == null)
                {
                    var worldGen = GetComponent<WorldGenerator>();

                    var worldProperties = new WorldProperties
                    {
                        ID = ID,
                        ChunkSize = new GridSize(CHUNK_SIZE),
                        WorldGenerator = worldGen,
                    };

                    var blockList = GetComponent<BlockListManager>();
                    m_UnityWorldBuilder = new UnityWorldBuilder(transform, blockList, worldProperties);

                    var renderDistance = GetComponent<VoxelRenderDistance>();
                    renderDistance?.LoadPatternIterator.Reset();
                }

                return m_UnityWorldBuilder;
            }
            set => m_UnityWorldBuilder = value;
        }

        /// <summary>
        /// Gets the chunk size of this world.
        /// </summary>
        public GridSize ChunkSize => WorldBuilder.ChunkSize;

        /// <summary>
        /// Gets the number of active tasks being run on the block world server thread.
        /// </summary>
        public int ActiveTasks => WorldBuilder.ActiveTasks;

#if UNITY_EDITOR
        /// <summary>
        /// Called when the world is enabled to subscribe to editor frame updates
        /// and initialize.
        /// </summary>
        protected void OnEnable()
        {
            if (!Application.isPlaying)
                UnityEditor.EditorApplication.update += Update;
        }

        /// <summary>
        /// Called when the world is disabled to unsubscribe from editor frame updates.
        /// </summary>
        protected void OnDisable()
        {
            if (!Application.isPlaying)
            {
                UnityEditor.EditorApplication.update -= Update;
                SaveWorld();
            }
        }
#endif

        /// <summary>
        /// Called when this block world behaviour is destroyed.
        /// </summary>
        protected void OnDestroy()
        {
            WorldBuilder.Shutdown();
            WorldBuilder = null;
        }

        /// <summary>
        /// Applies an edit batch to this world, remeshing chunks as needed.
        /// </summary>
        /// <param name="editBatch">The edit batch to apply.</param>
        public void SetBlocks(IEditBatch editBatch) => WorldBuilder.SetBlocks(editBatch.GetBlocks);

        /// <summary>
        /// Applies an edit batch to this world, remeshing chunks as needed.
        /// </summary>
        /// <param name="editBatch">The edit batch to apply.</param>
        public void SetBlocks(EditBatch editBatch) => WorldBuilder.SetBlocks(editBatch);

        /// <summary>
        /// Sets a world in the world to a given ID.
        /// </summary>
        /// <param name="blockPos">The block position.</param>
        /// <param name="blockID">The ID of the block to place.</param>
        public void SetBlock(BlockPosition blockPos, ushort blockID) => WorldBuilder.SetBlock(blockPos, blockID);

        /// <summary>
        /// Gets the block type at the given world position.
        /// 
        /// For ungenerated or unloaded chunks, the Ungenerated block type is return.
        /// </summary>
        /// <param name="blockPos">The position of the block.</param>
        /// <param name="createChunk">Whether or not to create (or load) the chunk if it doesn't currently exist.</param>
        /// <returns>The block type.</returns>
        public ushort GetBlock(BlockPosition blockPos, bool createChunk = false) => WorldBuilder.GetBlock(blockPos, createChunk);

        /// <summary>
        /// Called each frame to pull remesh tasks from the remesh handler.
        /// </summary>
        protected void Update() => WorldBuilder.Update();

        /// <summary>
        /// Saves the world to file.
        /// </summary>
        public void SaveWorld() => WorldBuilder.SaveWorld();

        /// <summary>
        /// Force loads all chunks within a given region, if not already loaded.
        /// </summary>
        /// <param name="center">The center of the bounding region.</param>
        /// <param name="extents">The radius of each axis.</param>
        /// <returns>True if any additional chunks were loaded.</returns>
        public bool LoadChunkRegion(ChunkPosition center, Vector3Int extents) => WorldBuilder.LoadChunkRegion(center, extents);

        /// <summary>
        /// Requests the chunk at the given position to start loading in the background.
        /// </summary>
        /// <param name="chunkPos">The chunk position.</param>
        public void LoadChunkAsync(ChunkPosition chunkPos) => WorldBuilder.LoadChunkAsync(chunkPos);

        /// <summary>
        /// Preforms a raycast in the scene. If the ray hits this block world, returns the block that was hit.
        /// </summary>
        /// <param name="ray">The ray.</param>
        /// <param name="maxDistance">The max distance of the raycast.</param>
        /// <returns>The target block data.</returns>
        public TargetBlock RaycastWorld(Ray ray, float maxDistance) => RaycastWorld(ray, maxDistance, Physics.DefaultRaycastLayers);

        /// <summary>
        /// Preforms a raycast in the scene and returns the block that was hit, if any.
        /// </summary>
        /// <param name="ray">The ray.</param>
        /// <param name="maxDistance">The max distance of the raycast.</param>
        /// <param name="layerMask">The layer mask for the raycast.</param>
        /// <returns>The target block data.</returns>
        public TargetBlock RaycastWorld(Ray ray, float maxDistance, LayerMask layerMask)
        {
            if (!Physics.Raycast(ray, out RaycastHit hit, maxDistance, layerMask))
                return new TargetBlock();

            // Shift hit location to avoid floating point errors
            Vector3 inside = hit.point - ray.direction * .0001f;
            Vector3 over = hit.point + ray.direction * .0001f;

            var target = new TargetBlock
            {
                Inside = VectorToBlockPos(inside),
                Over = VectorToBlockPos(over),
                HasBlock = true,
                Side = 0,
            };

            return target;
        }

        /// <summary>
        /// Converts a position in the Unity scene to the block position the point is in.
        /// </summary>
        /// <param name="pos">The point in world space.</param>
        /// <returns>The block position containing this point.</returns>
        public BlockPosition VectorToBlockPos(Vector3 pos)
        {
            pos = transform.InverseTransformPoint(pos);
            var blockPos = new BlockPosition
            {
                X = Mathf.FloorToInt(pos.x),
                Y = Mathf.FloorToInt(pos.y),
                Z = Mathf.FloorToInt(pos.z),
            };

            return blockPos;
        }
    }
}
