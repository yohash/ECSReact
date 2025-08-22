using System;
using System.Linq.Expressions;
using System.Collections.Generic;
using Unity.Entities;

namespace ECSReact.Editor
{
  public static class ComponentDataGetter
  {
    private static readonly Dictionary<Type, Func<EntityManager, Entity, object>> getters = new();

    public static object GetComponentData(EntityManager entityManager, Entity entity, Type componentType)
    {
      if (!getters.TryGetValue(componentType, out var getter)) {
        getter = CreateGetter(componentType);
        getters[componentType] = getter;
      }
      return getter(entityManager, entity);
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
}