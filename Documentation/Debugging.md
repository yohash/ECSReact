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
