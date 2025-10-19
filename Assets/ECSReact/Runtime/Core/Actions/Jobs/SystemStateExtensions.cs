using Unity.Entities;
using Unity.Burst;
using System.Runtime.CompilerServices;

namespace ECSReact.Core
{
  /// <summary>
  /// Extension methods for SystemState to provide SystemAPI-like convenience methods
  /// when working with IReducer and IMiddleware interfaces.
  /// These methods make singleton access cleaner and more intuitive.
  /// </summary>
  [BurstCompile]
  public static class SystemStateExtensions
  {
    /// <summary>
    /// Tries to get a singleton component. Returns false if the singleton doesn't exist
    /// or if there are multiple entities with the component.
    /// </summary>
    /// <typeparam name="T">The singleton component type</typeparam>
    /// <param name="systemState">The system state</param>
    /// <param name="singleton">The singleton component data if found</param>
    /// <returns>True if exactly one entity with the component exists, false otherwise</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryGetSingleton<T>(this ref SystemState systemState, out T singleton)
        where T : unmanaged, IComponentData
    {
      var query = systemState.GetEntityQuery(ComponentType.ReadOnly<T>());

      if (!query.IsEmpty && query.CalculateEntityCount() == 1) {
        var entity = query.GetSingletonEntity();
        singleton = systemState.EntityManager.GetComponentData<T>(entity);
        return true;
      }

      singleton = default;
      return false;
    }

    /// <summary>
    /// Gets a singleton component. Throws if the singleton doesn't exist or if there are
    /// multiple entities with the component.
    /// </summary>
    /// <typeparam name="T">The singleton component type</typeparam>
    /// <param name="systemState">The system state</param>
    /// <returns>The singleton component data</returns>
    /// <exception cref="System.InvalidOperationException">Thrown if not exactly one entity has the component</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T GetSingleton<T>(this ref SystemState systemState)
        where T : unmanaged, IComponentData
    {
      var query = systemState.GetEntityQuery(ComponentType.ReadOnly<T>());
      var entity = query.GetSingletonEntity(); // Will throw if not exactly one
      return systemState.EntityManager.GetComponentData<T>(entity);
    }

    /// <summary>
    /// Checks if a singleton component exists (exactly one entity has the component).
    /// </summary>
    /// <typeparam name="T">The singleton component type</typeparam>
    /// <param name="systemState">The system state</param>
    /// <returns>True if exactly one entity has the component, false otherwise</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool HasSingleton<T>(this ref SystemState systemState)
        where T : unmanaged, IComponentData
    {
      var query = systemState.GetEntityQuery(ComponentType.ReadOnly<T>());
      return !query.IsEmpty && query.CalculateEntityCount() == 1;
    }

    /// <summary>
    /// Gets the entity that has the singleton component. Throws if not exactly one exists.
    /// </summary>
    /// <typeparam name="T">The singleton component type</typeparam>
    /// <param name="systemState">The system state</param>
    /// <returns>The entity with the singleton component</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Entity GetSingletonEntity<T>(this ref SystemState systemState)
        where T : unmanaged, IComponentData
    {
      var query = systemState.GetEntityQuery(ComponentType.ReadOnly<T>());
      return query.GetSingletonEntity();
    }

    /// <summary>
    /// Gets a component lookup for efficient entity component access.
    /// Useful when you need to access components on multiple entities.
    /// </summary>
    /// <typeparam name="T">The component type</typeparam>
    /// <param name="systemState">The system state</param>
    /// <param name="isReadOnly">Whether the lookup should be read-only</param>
    /// <returns>A ComponentLookup for the specified component type</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ComponentLookup<T> GetComponentLookup<T>(this ref SystemState systemState, bool isReadOnly = true)
        where T : unmanaged, IComponentData
    {
      return systemState.GetComponentLookup<T>(isReadOnly);
    }

    /// <summary>
    /// Gets a buffer lookup for efficient entity buffer access.
    /// </summary>
    /// <typeparam name="T">The buffer element type</typeparam>
    /// <param name="systemState">The system state</param>
    /// <param name="isReadOnly">Whether the lookup should be read-only</param>
    /// <returns>A BufferLookup for the specified buffer type</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BufferLookup<T> GetBufferLookup<T>(this ref SystemState systemState, bool isReadOnly = true)
        where T : unmanaged, IBufferElementData
    {
      return systemState.GetBufferLookup<T>(isReadOnly);
    }

    /// <summary>
    /// Gets a singleton buffer. Throws if the singleton doesn't exist or if there are
    /// multiple entities with the buffer.
    /// </summary>
    /// <typeparam name="T">The buffer element type</typeparam>
    /// <param name="systemState">The system state</param>
    /// <returns>The singleton buffer</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static DynamicBuffer<T> GetSingletonBuffer<T>(this ref SystemState systemState)
        where T : unmanaged, IBufferElementData
    {
      var query = systemState.GetEntityQuery(ComponentType.ReadOnly<T>());
      var entity = query.GetSingletonEntity();
      return systemState.EntityManager.GetBuffer<T>(entity);
    }

    /// <summary>
    /// Tries to get a singleton buffer. Returns false if the singleton doesn't exist
    /// or if there are multiple entities with the buffer.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryGetSingletonBuffer<T>(this ref SystemState systemState, out DynamicBuffer<T> buffer)
        where T : unmanaged, IBufferElementData
    {
      var query = systemState.GetEntityQuery(ComponentType.ReadOnly<T>());

      if (!query.IsEmpty && query.CalculateEntityCount() == 1) {
        var entity = query.GetSingletonEntity();
        buffer = systemState.EntityManager.GetBuffer<T>(entity);
        return true;
      }

      buffer = default;
      return false;
    }

    /// <summary>
    /// Creates an EntityCommandBuffer using the specified ECB system.
    /// Useful for deferred structural changes in Burst-compiled code.
    /// </summary>
    /// <typeparam name="T">The EntityCommandBufferSystem type</typeparam>
    /// <param name="systemState">The system state</param>
    /// <returns>An EntityCommandBuffer from the specified system</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static EntityCommandBuffer CreateCommandBuffer<T>(this ref SystemState systemState)
        where T : EntityCommandBufferSystem
    {
      var ecbSystem = systemState.World.GetExistingSystemManaged<T>();
      return ecbSystem.CreateCommandBuffer();
    }

    /// <summary>
    /// Gets an EntityCommandBuffer singleton for deferred operations.
    /// This assumes you've set up an ECB singleton pattern in your project.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static EntityCommandBuffer GetCommandBuffer(this ref SystemState systemState)
    {
      var ecbSingleton = systemState.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
      return ecbSingleton.CreateCommandBuffer(systemState.WorldUnmanaged);
    }

    /// <summary>
    /// Checks if a component type exists on a specific entity.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool HasComponent<T>(this ref SystemState systemState, Entity entity)
        where T : unmanaged, IComponentData
    {
      return systemState.EntityManager.HasComponent<T>(entity);
    }

    /// <summary>
    /// Gets a component from a specific entity.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T GetComponent<T>(this ref SystemState systemState, Entity entity)
        where T : unmanaged, IComponentData
    {
      return systemState.EntityManager.GetComponentData<T>(entity);
    }

    /// <summary>
    /// Get the current elapsed time from WorldUnmanaged.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double GetElapsedTime(this ref SystemState systemState)
    {
      return systemState.WorldUnmanaged.Time.ElapsedTime;
    }

    /// <summary>
    /// Get the current delta time from WorldUnmanaged.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float GetDeltaTime(this ref SystemState systemState)
    {
      return systemState.WorldUnmanaged.Time.DeltaTime;
    }
  }
}