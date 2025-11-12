using UnityEngine;

namespace ECSReact.Samples.SimpleSetup
{
  /// <summary>
  /// A simple initialization script for demonstration purposes.
  /// </summary>
  public class InitializationScript : MonoBehaviour
  {
    private void Awake()
    {
      Namespace2.State.StateNotificationEvents.InitializeEvents();
      Namespace2.State.StateSubscriptionRegistration.InitializeSubscriptions();

      Namespace1.State.StateNotificationEvents.InitializeEvents();
      Namespace1.State.StateSubscriptionRegistration.InitializeSubscriptions();
    }
  }
}