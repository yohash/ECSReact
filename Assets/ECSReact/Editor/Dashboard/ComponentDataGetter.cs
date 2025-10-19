using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using Unity.Entities;

namespace ECSReact.Editor
{
  public static class ComponentDataGetter
  {
    private static readonly Dictionary<Type, Func<EntityManager, Entity, object>> getters = new();

    // Cache for zero-sized component types to avoid repeated size checks
    private static readonly HashSet<Type> zeroSizedTypes = new();
    private static readonly HashSet<Type> nonZeroSizedTypes = new();

    public static object GetComponentData(EntityManager entityManager, Entity entity, Type componentType)
    {
      // Fast path: check cached zero-sized types first
      if (zeroSizedTypes.Contains(componentType)) {
        return CreateZeroSizedComponentPlaceholder(componentType);
      }

      // Fast path: check cached non-zero-sized types
      if (nonZeroSizedTypes.Contains(componentType)) {
        if (!getters.TryGetValue(componentType, out var getter)) {
          getter = CreateGetter(componentType);
          getters[componentType] = getter;
        }
        return getter(entityManager, entity);
      }

      // First time seeing this type - check if it's zero-sized
      if (IsZeroSizedComponent(componentType)) {
        zeroSizedTypes.Add(componentType);
        return CreateZeroSizedComponentPlaceholder(componentType);
      }

      // Not zero-sized, cache it and proceed normally
      nonZeroSizedTypes.Add(componentType);
      if (!getters.TryGetValue(componentType, out var normalGetter)) {
        normalGetter = CreateGetter(componentType);
        getters[componentType] = normalGetter;
      }
      return normalGetter(entityManager, entity);
    }

    private static bool IsZeroSizedComponent(Type componentType)
    {
      // Method 1: Check if the struct has any instance fields
      // Empty tag components typically have no fields
      var fields = componentType.GetFields(System.Reflection.BindingFlags.Instance |
                                          System.Reflection.BindingFlags.Public |
                                          System.Reflection.BindingFlags.NonPublic);

      if (fields.Length == 0) {
        // No fields = likely a tag component that Unity treats as zero-sized
        return true;
      }

      // Method 2: Use Unity's TypeManager to check if registered as zero-sized
      try {
        var typeIndex = TypeManager.GetTypeIndex(componentType);
        var componentType2 = ComponentType.FromTypeIndex(typeIndex);
        return componentType2.IsZeroSized;
      } catch {
        // TypeManager might throw if type isn't registered yet
        // Fall back to testing with a dummy entity
      }

      // Method 3: Fallback - actually test if GetComponentData throws
      // This is the most reliable but requires creating a test entity
      return TestComponentDataAccess(componentType);
    }

    private static bool TestComponentDataAccess(Type componentType)
    {
      try {
        // This is a bit expensive but most reliable - create a test scenario
        // We'll cache the result so this only happens once per type
        var world = World.DefaultGameObjectInjectionWorld;
        if (world == null || !world.IsCreated)
          return false;

        var em = world.EntityManager;
        var testEntity = em.CreateEntity();

        try {
          // Add the component to test entity
          var addComponentMethod = typeof(EntityManager)
            .GetMethod("AddComponent", new Type[] { typeof(Entity) })
            ?.MakeGenericMethod(componentType);

          addComponentMethod?.Invoke(em, new object[] { testEntity });

          // Try to get component data
          var getComponentMethod = typeof(EntityManager)
            .GetMethod("GetComponentData", new Type[] { typeof(Entity) })
            ?.MakeGenericMethod(componentType);

          getComponentMethod?.Invoke(em, new object[] { testEntity });

          // If we get here, it's not zero-sized
          return false;
        } catch (Exception ex) {
          // If we get the "zero sized component" error or similar, it's zero-sized
          return ex.Message.Contains("zero sized component") ||
                 ex.Message.Contains("zero-sized") ||
                 ex is TargetParameterCountException;
        } finally {
          // Clean up test entity
          if (em.Exists(testEntity)) {
            em.DestroyEntity(testEntity);
          }
        }
      } catch {
        // If anything goes wrong with the test, assume it's not zero-sized
        return false;
      }
    }

    private static object CreateZeroSizedComponentPlaceholder(Type componentType)
    {
      // For zero-sized components, we can return:
      // 1. A default instance of the struct
      // 2. A special placeholder object
      // 3. null (if that works for your debugging needs)

      try {
        // Option 1: Return a default instance of the struct
        return Activator.CreateInstance(componentType);
      } catch {
        // Option 2: Return a placeholder object with type information
        return new ZeroSizedComponentPlaceholder
        {
          ComponentTypeName = componentType.Name,
          ComponentFullName = componentType.FullName,
          IsZeroSized = true
        };
      }
    }

    private static Func<EntityManager, Entity, object> CreateGetter(Type componentType)
    {
      // Create a compiled lambda: (em, e) => (object)em.GetComponentData<T>(e)
      var emParam = Expression.Parameter(typeof(EntityManager), "em");
      var entityParam = Expression.Parameter(typeof(Entity), "e");

      var getComponentMethod = typeof(EntityManager)
          .GetMethod("GetComponentData", new Type[] { typeof(Entity) })
          .MakeGenericMethod(componentType);

      var callExpr = Expression.Call(
          emParam,
          getComponentMethod,
          entityParam
      );

      var boxedExpr = Expression.Convert(callExpr, typeof(object));

      var lambda = Expression.Lambda<Func<EntityManager, Entity, object>>(
          boxedExpr,
          emParam,
          entityParam
      );

      return lambda.Compile();
    }
  }

  /// <summary>
  /// Placeholder object returned for zero-sized components that can't have their data retrieved
  /// </summary>
  public class ZeroSizedComponentPlaceholder
  {
    public string ComponentTypeName { get; set; }
    public string ComponentFullName { get; set; }
    public bool IsZeroSized { get; set; } = true;

    public override string ToString()
    {
      return $"[Zero-sized component: {ComponentTypeName}]";
    }
  }
}