# Debugging Tools

| Tool | Type | Purpose & Access |
| --- | --- | --- |
| **State Viewer** | Editor Window<br>_Runtime Tool_ | Live state inspection, action history, test dispatch<br>*Access: ECSReact → State Viewer* |
| **Code Generators** | Editor Windows | Generate UIStateNotifier, subscriptions, store extensions<br>*Access: ECSReact → Generate [Type]* |
| **Auto Generate All** | Editor Window | One-click generation for all systems<br>*Access: ECSReact → Auto Generate All* |
| **UIEventDebugger** | MonoBehaviour | Monitor UI event queue performance<br>*Access: Add to GameObject, enable logging* |
| **UISubscriptionDebugger** | MonoBehaviour | Track UI component state subscriptions<br>*Access: Add to GameObject, configure filters* |
| **SystemUpdateGroupDebugger** | MonoBehaviour | Monitor ECS system execution order & timing<br>*Access: Add to GameObject, enable logging* |
| **Scene State Manager** | MonoBehaviour | Inspect/configure state singletons per scene<br>*Access: Inspector on SceneStateManager component* |

## Element Debugging

### **Element Lifecycle Tracking**

To debug element mounting/unmounting issues, you can add logging to your `DeclareElements()` method:

```csharp
protected override IEnumerable<UIElement> DeclareElements()
{
    Debug.Log($"[{GetType().Name}] Declaring elements for state: {currentState}");
    
    foreach (var element in GenerateElements())
    {
        Debug.Log($"[{GetType().Name}] Declaring element: {element.Key}");
        yield return element;
    }
}
```

### **Props Debugging**

Track props changes in components implementing `IElement`:

```csharp
public void UpdateProps(UIProps props)
{
    var oldProps = this.props;
    this.props = props as MyProps;
    
    Debug.Log($"[{GetType().Name}] Props updated: {oldProps} → {this.props}");
    UpdateDisplay();
}
```

### **Element Hierarchy Inspector**

Add this helper to any `ReactiveUIComponent` to debug element structure:

```csharp
[ContextMenu("Debug Element Hierarchy")]
private void DebugElementHierarchy()
{
    Debug.Log($"=== Element Hierarchy for {GetType().Name} ===");
    
    var elements = DeclareElements().ToList();
    for (int i = 0; i < elements.Count; i++)
    {
        var element = elements[i];
        Debug.Log($"  [{i}] Key: {element.Key}, Index: {element.Index}, Props: {element.Props?.GetType().Name ?? "null"}");
        
        if (element.Component != null)
        {
            Debug.Log($"      Component: {element.Component.GetType().Name}");
            Debug.Log($"      GameObject: {element.GameObject?.name ?? "null"}");
        }
    }
}
```

### **Performance Monitoring**

Track element update performance:

```csharp
public class ElementPerformanceTracker : MonoBehaviour
{
    [SerializeField] private bool enableTracking = true;
    [SerializeField] private float reportInterval = 5f;
    
    private Dictionary<string, int> elementCounts = new();
    private float lastReportTime;
    
    void Update()
    {
        if (!enableTracking) return;
        
        if (Time.time - lastReportTime > reportInterval)
        {
            ReportElementStats();
            lastReportTime = Time.time;
        }
    }
    
    public void TrackElementUpdate(string componentType, int elementCount)
    {
        if (!enableTracking) return;
        elementCounts[componentType] = elementCount;
    }
    
    private void ReportElementStats()
    {
        Debug.Log("=== Element Performance Report ===");
        foreach (var kvp in elementCounts.OrderByDescending(x => x.Value))
        {
            Debug.Log($"  {kvp.Key}: {kvp.Value} elements");
        }
    }
}
```

## Next

1. [Overview](Overview.md)
2. [Architecture](Architecture.md)
3. [Setup](Setup.md)
4. [API](API.md)
5. Debugging Tools
6. [Examples & Patterns](Examples.md)