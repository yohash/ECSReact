# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## \[0.2.0] - 2026-11-12

### Added

###### Fixed-step action lane: `[FixedStepAction]`

Action types marked `[FixedStepAction]` are processed at the end of every fixed step instead of once per rendered frame, so gameplay actions resolve a fixed number of steps after dispatch whatever the render rate. A new `FixedActionSystemGroup` runs last in `FixedStepSimulationSystemGroup` (after `EndFixedStepSimulationEntityCommandBufferSystem`) and contains `FixedActionCommandBufferSystem`, `FixedMiddlewareSystemGroup`, `FixedReducerSystemGroup` and `FixedActionCleanupSystem`. Fixed-lane entities carry `FixedActionTag` instead of `ActionTag`. `ECSActionDispatcher.Dispatch` and `Store.Dispatch` route by lane automatically; jobs use the new `ECSActionDispatcher.GetFixedJobCommandBuffer()`. `ActionLane<T>.IsFixed` exposes the lane to Burst code. See *API: Action Lanes* for the timing rules.

###### The debug dashboard now captures fixed-lane actions (`FixedLaneDebugActionInterceptorSystem`)

###### `BattleSystemDemo` sample: a fixed-step turn clock (`TurnClockSystem`, `TurnClockTickAction`, `TurnClockReducer`)

### Changed

###### **Breaking:** `IMiddleware.Process` takes `ref ActionWriter dispatch`

The `(EntityCommandBuffer.ParallelWriter ecb, int sortKey)` parameters are replaced by a lane-routing `ActionWriter`. Replace `ecb.DispatchAction(sortKey, x)` with `dispatch.Dispatch(x)`; use `dispatch.Ecb` with `dispatch.NextSortKey()` for non-action command buffer work. See *Setup: Upgrading to 0.2.0*.

###### **Breaking:** generated code changed; regenerate after upgrading

###### `Store.Dispatch` now delegates to `ECSActionDispatcher.Dispatch`

### Fixed

###### Dispatching while paused (`Time.timeScale = 0`) reused an already played-back command buffer

The dispatcher cached its command buffer until `World.Time.ElapsedTime` advanced, which never happens while paused, so the next frame's dispatches went into a buffer that had already been played back and disposed. Buffers are now replaced whenever their command buffer system updates, in both lanes. `Store`'s own `Time.frameCount`-based cache is removed.

###### Action buffers could be played back while a registered job was still writing to them

`ECSActionDispatcher.RegisterJobHandle` only completed handles after `JobActionCollectorSystem` had already played the buffer back. Handles are now registered with each command buffer system via `AddJobHandleForProducer`, so playback waits for the job. This raised a safety exception in the Editor and was a data race in builds.

###### Middleware dispatching more than one action reused sort keys (`sortKey + 1` collided with the next action's key)

### Removed

###### Stale `IParallelReducer` / `IParallelMiddleware` and `PhysicsMiddlewareSystemGroup` references from the docs

## \[0.1.6] - 2026-05-23

### Fixed

###### `StateRegistryGenerator` now discovers all `IGameState` types across all assemblies

The editor tool was silently missing state types that `UIStateNotifierGenerator` found correctly. Two bugs in `discoverStateTypes()`: an assembly name filter that could exclude valid user assemblies, and a broken reflection-based `IsUnmanaged()` check that incorrectly rejected states containing Unity structs with internal pointer fields (e.g. `BlobAssetReference<T>`). Discovery logic is now fully aligned with `UIStateNotifierGenerator`.

## \[0.1.5] - 2026-03-18

### Changed

###### Removed timestamps from generated code

###### Sort state containers in ECSReactDashboard in alphabetical order

## \[0.1.4] - 2026-03-06

### Added

###### Added a new method `IMiddleware.OnCreate()` for initialization

### Removed

###### Actions in middleware are no longer mutable using `in Action action`

## \[0.1.3] - 2026-02-11

### Changed

###### Moved the `Store.Start()` method contents into `Awake()`

### Removed

###### Removed an irrelevant entry in `SystemUpdateGroups.cs`


## \[0.1.2] - 2025-11-12

### Added

###### Added a new sample scene "SimpleSetup"


### Fixed

###### Added cross-namespace support to the ISystemCodeGeneration

### Removed

###### Removed `IParallelReducer` and `IParallelMiddleware`

## \[0.1.1] - 2025-11-5

### Removed

###### Removed the store extensions code generator



## \[0.1.0] - 2025-10-23

### Added

###### Created project

