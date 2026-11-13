using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Unity.Entities;
using ECSReact.Core;

namespace ECSReact.Tests
{
  // ───────────────────────────── Fixtures ─────────────────────────────

  public struct PlacementTestState : IGameState, IEquatable<PlacementTestState>
  {
    public int value;
    public bool Equals(PlacementTestState other) => value == other.value;
  }

  public class PlacementTestStateChangedEvent : UIEvent { }

  /// <summary>
  /// Declared exactly as StateSubscriptionHelperGenerator emits a notification system
  /// (minus [BurstCompile]), so the placement test checks the generator's attribute.
  /// </summary>
  [DisableAutoCreation]
  [UpdateInGroup(typeof(UINotificationSystemGroup))]
  public partial class PlacementTestStateChangeNotificationSystem : StateChangeNotificationSystem<PlacementTestState>
  {
    protected override UIEvent CreateStateChangeEvent(
      PlacementTestState newState, PlacementTestState oldState, bool hasOldState) => new PlacementTestStateChangedEvent();
  }

  /// <summary>
  /// Issue #16: Entities' codegen type registry matches UpdateInGroup / UpdateBefore / UpdateAfter
  /// by exact type, so a subclass of those attributes is ignored and the system lands at the root of
  /// SimulationSystemGroup. These tests keep system placement honest.
  /// </summary>
  public class SystemGroupPlacementTests
  {
    private World world;

    [TearDown]
    public void TearDown()
    {
      if (world != null && world.IsCreated)
        world.Dispose();
      world = null;
    }

    /// <summary>A state-change notification system must run in UINotificationSystemGroup (PresentationSystemGroup).</summary>
    [Test]
    public void StateChangeNotificationSystem_IsPlacedInUINotificationSystemGroup()
    {
      world = new World("ECSReact Placement Test World");
      DefaultWorldInitialization.AddSystemsToRootLevelSystemGroups(world, new[]
      {
        typeof(UINotificationSystemGroup),
        typeof(PlacementTestStateChangeNotificationSystem),
      });

      var notificationGroup = world.GetExistingSystemManaged<UINotificationSystemGroup>();
      var presentation = world.GetExistingSystemManaged<PresentationSystemGroup>();
      var simulation = world.GetExistingSystemManaged<SimulationSystemGroup>();

      Assert.That(presentation.ManagedSystems, Has.Member(notificationGroup),
        "UINotificationSystemGroup must be in PresentationSystemGroup");
      Assert.That(notificationGroup.ManagedSystems.Select(s => s.GetType()),
        Has.Member(typeof(PlacementTestStateChangeNotificationSystem)),
        "the notification system is not in UINotificationSystemGroup");
      Assert.That(simulation.ManagedSystems.Select(s => s.GetType()),
        Has.No.Member(typeof(PlacementTestStateChangeNotificationSystem)),
        "the notification system landed at the root of SimulationSystemGroup");
    }

    /// <summary>
    /// No system in an ECSReact assembly may use a subclass of an ordering attribute: Entities
    /// silently ignores it. Covers the runtime, the tests, and the samples' generated systems
    /// whenever the samples are imported.
    /// </summary>
    [Test]
    public void NoSystem_UsesASubclassOfAnOrderingAttribute()
    {
      var orderingAttributes = new[]
      {
        typeof(UpdateInGroupAttribute),
        typeof(UpdateBeforeAttribute),
        typeof(UpdateAfterAttribute),
      };

      var offenders = new List<string>();
      var assemblies = AppDomain.CurrentDomain.GetAssemblies()
        .Where(a => a.GetName().Name.StartsWith("ECSReact", StringComparison.Ordinal));

      foreach (var assembly in assemblies) {
        Type[] types;
        try {
          types = assembly.GetTypes();
        } catch (System.Reflection.ReflectionTypeLoadException e) {
          types = e.Types.Where(t => t != null).ToArray();
        }

        foreach (var type in types) {
          bool isSystem = typeof(ComponentSystemBase).IsAssignableFrom(type) || typeof(ISystem).IsAssignableFrom(type);
          if (!isSystem)
            continue;

          foreach (var attribute in type.GetCustomAttributes(inherit: false)) {
            var attributeType = attribute.GetType();
            var baseOrdering = orderingAttributes.FirstOrDefault(o => o != attributeType && o.IsAssignableFrom(attributeType));
            if (baseOrdering != null)
              offenders.Add($"{type.FullName} uses [{attributeType.Name}], a subclass of {baseOrdering.Name}");
          }
        }
      }

      CollectionAssert.IsEmpty(offenders,
        "Entities ignores subclasses of ordering attributes; use the base attribute directly:\n  " +
        string.Join("\n  ", offenders));
    }
  }
}
